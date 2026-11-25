using System.Data;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IClientRegistrationService
    {
        Task<ClientRegistrationResponseDto> SaveClientRegistrationAsync(ClientRegistrationRequestDto dto, CancellationToken cancellationToken = default);
        Task<ClientRegistrationResponseDto?> GetClientRegistrationByIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClientRegistrationResponseDto>> GetAllClientRegistrationsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ClientSearchRowDto>> SearchClientsAsync(ClientSearchFilterDto? filter = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClientDropdownOptionDto>> GetDropdownOptionsAsync(string mode, CancellationToken cancellationToken = default);
        Task<string> GenerateUniqueCustomerCodeAsync(string clientName, string? customerType = null, CancellationToken cancellationToken = default);
        Task<bool> AdvanceActivationStepAsync(AdvanceActivationStepDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateTechnicalConfigAsync(UpdateTechnicalConfigDto dto, CancellationToken cancellationToken = default);
        Task<ClientDocumentDto> UploadDocumentAsync(UploadClientDocumentDto dto, int actorUserId = 0, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClientDocumentDto>> GetDocumentsByClientIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<(byte[] FileBytes, string ContentType, string DownloadFileName)?> GetDocumentFileAsync(int documentId, CancellationToken cancellationToken = default);
        Task<bool> DeleteDocumentAsync(int documentId, int actorUserId = 1, CancellationToken cancellationToken = default);
    }

    public class ClientRegistrationService : IClientRegistrationService
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<ClientRegistrationService> _logger;
        private readonly string _baseStoragePath;

        public ClientRegistrationService(
            IDbConnectionFactory connectionFactory,
            ILogger<ClientRegistrationService> logger,
            IConfiguration configuration)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
            _baseStoragePath = configuration["DocumentStorage:BasePath"] ?? @"C:\ProwessCRM\Uploads\ClientDocuments";
        }

        private static string NormalizeYN(string? val, string defaultVal = "N")
        {
            if (string.IsNullOrWhiteSpace(val)) return defaultVal;
            var v = val.Trim().ToUpperInvariant();
            return (v == "Y" || v == "YES" || v == "TRUE" || v == "1") ? "Y" : "N";
        }

        private static string? NormalizeNullableYN(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return null;
            var v = val.Trim().ToUpperInvariant();
            if (v == "Y" || v == "YES" || v == "TRUE" || v == "1") return "Y";
            if (v == "N" || v == "NO" || v == "FALSE" || v == "0") return "N";
            return null;
        }

        private static object DbVal(object? val) => val ?? DBNull.Value;

        private static object DbStr(string? val) => string.IsNullOrWhiteSpace(val) ? DBNull.Value : val.Trim();

        public async Task<ClientRegistrationResponseDto> SaveClientRegistrationAsync(
            ClientRegistrationRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var transaction = (SqlTransaction)await conn.BeginTransactionAsync(cancellationToken);

            try
            {
                int clientId = dto.ClientId.GetValueOrDefault(0);
                bool isNew = clientId <= 0;
                if (dto.CreatedBy <= 0)
                {
                    throw new ArgumentException("CreatedBy user ID is required and must be greater than 0.");
                }
                int actorUserId = dto.CreatedBy;
                string isActive = NormalizeYN(dto.IsActive, "Y");

                // Enforce exact CK_Client_Master_Stage values: ('DRAFT', 'PENDING_TECH', 'COMPLETED')
                string rawStage = string.IsNullOrWhiteSpace(dto.RegistrationStage) ? "DRAFT" : dto.RegistrationStage.Trim().ToUpperInvariant();
                string stage;
                if (rawStage == "REGISTERED" || rawStage == "COMPLETED" || rawStage == "ACTIVE")
                {
                    stage = "COMPLETED";
                }
                else if (rawStage == "PENDING_TECH" || rawStage == "INCOMPLETE" || rawStage == "TECH_PENDING" || rawStage == "SUBMITTED" || rawStage == "ADMIN_SUBMITTED")
                {
                    stage = "PENDING_TECH";
                }
                else
                {
                    stage = "DRAFT";
                }

                bool isAdminSubmitted = stage == "PENDING_TECH" || stage == "COMPLETED";

                // 0. Duplicate check on Client_Name / PAN_No / GSTIN_Primary inside the transaction
                const string dupSql = @"
                    SELECT TOP 1 Client_Id, Client_Name, Customer_Code, PAN_No, GSTIN_Primary
                    FROM dbo.Client_Master
                    WHERE (@ClientId <= 0 OR Client_Id <> @ClientId)
                      AND (
                            LOWER(LTRIM(RTRIM(Client_Name))) = LOWER(LTRIM(RTRIM(@ClientName)))
                            OR (@PanNo IS NOT NULL AND PAN_No = @PanNo)
                            OR (@GstinPrimary IS NOT NULL AND GSTIN_Primary = @GstinPrimary)
                          );";

                using (var dupCmd = new SqlCommand(dupSql, conn, transaction))
                {
                    dupCmd.Parameters.AddWithValue("@ClientId", clientId);
                    dupCmd.Parameters.AddWithValue("@ClientName", dto.ClientName.Trim());
                    dupCmd.Parameters.AddWithValue("@PanNo", DbStr(dto.PanNo));
                    dupCmd.Parameters.AddWithValue("@GstinPrimary", DbStr(dto.GstinPrimary));

                    using var dupReader = await dupCmd.ExecuteReaderAsync(cancellationToken);
                    if (await dupReader.ReadAsync(cancellationToken))
                    {
                        var existingName = dupReader["Client_Name"]?.ToString();
                        var existingCode = dupReader["Customer_Code"]?.ToString() ?? $"ID {dupReader["Client_Id"]}";
                        throw new InvalidOperationException($"Duplicate customer detected: matches existing record {existingCode} ({existingName}).");
                    }
                }

                // =========================================================================
                // 1. SAVE TO dbo.Client_Master (Basic Information + Statutory & GST)
                // =========================================================================
                string customerCode = (dto.CustomerCode ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(customerCode) ||
                    customerCode.StartsWith("Auto", StringComparison.OrdinalIgnoreCase) ||
                    customerCode.StartsWith("CUS-", StringComparison.OrdinalIgnoreCase))
                {
                    customerCode = await GenerateUniqueCustomerCodeInternalAsync(conn, transaction, dto.ClientName, dto.CustomerType, cancellationToken);
                }

                if (isNew)
                {
                    const string insertMasterSql = @"
                        INSERT INTO dbo.Client_Master (
                            Client_Name, Client_Address, Is_Active,
                            Customer_Code, Display_Name, Customer_Type, Website, Year_Of_Establishment,
                            Industry_Category, Business_Segment, Company_Size, Number_Of_Branches,
                            Customer_Source, Relationship_Manager_Id, Customer_Status, Registration_Date, Internal_Remarks,
                            PAN_No, GST_Registration_Type, GSTIN_Primary, CIN_No, LLPIN_No, TAN_No, MSME_Reg_No, IEC_No,
                            Billing_Address, Country, State, City, District, PIN_Code,
                            Registration_Stage, Admin_Submitted_By, Admin_Submitted_Date,
                            Created_By, Created_Date
                        )
                        OUTPUT INSERTED.Client_Id
                        VALUES (
                            @ClientName, @ClientAddress, @IsActive,
                            @CustomerCode, @DisplayName, @CustomerType, @Website, @YearOfEstablishment,
                            @IndustryCategory, @BusinessSegment, @CompanySize, @NumberOfBranches,
                            @CustomerSource, @RelationshipManagerId, @CustomerStatus, @RegistrationDate, @InternalRemarks,
                            @PanNo, @GstRegistrationType, @GstinPrimary, @CinNo, @LlpinNo, @TanNo, @MsmeRegNo, @IecNo,
                            @BillingAddress, @Country, @State, @City, @District, @PinCode,
                            @RegistrationStage, @AdminSubmittedBy, @AdminSubmittedDate,
                            @CreatedBy, GETDATE()
                        );";

                    object? scalar = null;
                    try
                    {
                        using var masterCmd = new SqlCommand(insertMasterSql, conn, transaction);
                        BindMasterParameters(masterCmd, dto, customerCode, isActive, stage, isAdminSubmitted, actorUserId);
                        scalar = await masterCmd.ExecuteScalarAsync(cancellationToken);
                    }
                    catch (SqlException ex) when (ex.Message.Contains("CK_Client_Master_Stage") && stage != "DRAFT")
                    {
                        using var retryCmd = new SqlCommand(insertMasterSql, conn, transaction);
                        BindMasterParameters(retryCmd, dto, customerCode, isActive, "DRAFT", isAdminSubmitted, actorUserId);
                        scalar = await retryCmd.ExecuteScalarAsync(cancellationToken);
                    }
                    clientId = Convert.ToInt32(scalar);
                }
                else
                {
                    const string updateMasterSql = @"
                        UPDATE dbo.Client_Master
                        SET Client_Name             = @ClientName,
                            Client_Address          = @ClientAddress,
                            Is_Active               = @IsActive,
                            Customer_Code           = COALESCE(NULLIF(@CustomerCode, ''), Customer_Code),
                            Display_Name            = @DisplayName,
                            Customer_Type           = @CustomerType,
                            Website                 = @Website,
                            Year_Of_Establishment   = @YearOfEstablishment,
                            Industry_Category       = @IndustryCategory,
                            Business_Segment        = @BusinessSegment,
                            Company_Size            = @CompanySize,
                            Number_Of_Branches      = @NumberOfBranches,
                            Customer_Source         = @CustomerSource,
                            Relationship_Manager_Id = @RelationshipManagerId,
                            Customer_Status         = @CustomerStatus,
                            Internal_Remarks        = @InternalRemarks,
                            PAN_No                  = @PanNo,
                            GST_Registration_Type   = @GstRegistrationType,
                            GSTIN_Primary           = @GstinPrimary,
                            CIN_No                  = @CinNo,
                            LLPIN_No                = @LlpinNo,
                            TAN_No                  = @TanNo,
                            MSME_Reg_No             = @MsmeRegNo,
                            IEC_No                  = @IecNo,
                            Billing_Address         = @BillingAddress,
                            Country                 = @Country,
                            State                   = @State,
                            City                    = @City,
                            District                = @District,
                            PIN_Code                = @PinCode,
                            Registration_Stage      = @RegistrationStage,
                            Admin_Submitted_By      = COALESCE(@AdminSubmittedBy, Admin_Submitted_By),
                            Admin_Submitted_Date    = COALESCE(@AdminSubmittedDate, Admin_Submitted_Date),
                            Tech_Submitted_By       = CASE WHEN @RegistrationStage = 'COMPLETED' THEN @CreatedBy ELSE Tech_Submitted_By END,
                            Tech_Submitted_Date     = CASE WHEN @RegistrationStage = 'COMPLETED' THEN GETDATE() ELSE Tech_Submitted_Date END,
                            Modified_By             = @CreatedBy,
                            Modified_Date           = GETDATE()
                        WHERE Client_Id = @ClientId;";

                    int affected = 0;
                    try
                    {
                        using var updateCmd = new SqlCommand(updateMasterSql, conn, transaction);
                        updateCmd.Parameters.AddWithValue("@ClientId", clientId);
                        BindMasterParameters(updateCmd, dto, customerCode, isActive, stage, isAdminSubmitted, actorUserId);
                        affected = await updateCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                    catch (SqlException ex) when (ex.Message.Contains("CK_Client_Master_Stage") && stage != "DRAFT")
                    {
                        using var retryUpdate = new SqlCommand(updateMasterSql, conn, transaction);
                        retryUpdate.Parameters.AddWithValue("@ClientId", clientId);
                        BindMasterParameters(retryUpdate, dto, customerCode, isActive, "DRAFT", isAdminSubmitted, actorUserId);
                        affected = await retryUpdate.ExecuteNonQueryAsync(cancellationToken);
                    }

                    if (affected == 0)
                    {
                        throw new InvalidOperationException($"Client_Id {clientId} was not found in dbo.Client_Master.");
                    }
                }

                // =========================================================================
                // 2. SAVE TO dbo.Client_Contact (1 client : many contacts)
                // =========================================================================
                if (!isNew)
                {
                    using var delContactsCmd = new SqlCommand("DELETE FROM dbo.Client_Contact WHERE Client_Id = @ClientId;", conn, transaction);
                    delContactsCmd.Parameters.AddWithValue("@ClientId", clientId);
                    await delContactsCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                if (dto.Contacts != null && dto.Contacts.Count > 0)
                {
                    bool primaryAssigned = false;

                    const string insertContactSql = @"
                        INSERT INTO dbo.Client_Contact (
                            Client_Id, Contact_Name, Designation, Department, Contact_Category,
                            Mobile_No, Alternate_Mobile_No, Email, Is_Primary, Is_Active,
                            Created_By, Created_Date
                        )
                        VALUES (
                            @ClientId, @ContactName, @Designation, @Department, @ContactCategory,
                            @MobileNo, @AlternateMobileNo, @Email, @IsPrimary, @IsActive,
                            @CreatedBy, GETDATE()
                        );";

                    for (int i = 0; i < dto.Contacts.Count; i++)
                    {
                        var c = dto.Contacts[i];
                        bool wantsPrimary = NormalizeYN(c.IsPrimary, "N") == "Y";
                        string isPrimary = "N";
                        if (wantsPrimary && !primaryAssigned)
                        {
                            isPrimary = "Y";
                            primaryAssigned = true;
                        }
                        else if (i == 0 && !dto.Contacts.Any(x => NormalizeYN(x.IsPrimary, "N") == "Y"))
                        {
                            isPrimary = "Y";
                            primaryAssigned = true;
                        }

                        using var contactCmd = new SqlCommand(insertContactSql, conn, transaction);
                        contactCmd.Parameters.AddWithValue("@ClientId", clientId);
                        contactCmd.Parameters.AddWithValue("@ContactName", c.ContactName.Trim());
                        contactCmd.Parameters.AddWithValue("@Designation", DbStr(c.Designation));
                        contactCmd.Parameters.AddWithValue("@Department", DbStr(c.Department));
                        contactCmd.Parameters.AddWithValue("@ContactCategory", string.IsNullOrWhiteSpace(c.ContactCategory) ? "Primary Contact" : c.ContactCategory.Trim());
                        contactCmd.Parameters.AddWithValue("@MobileNo", c.MobileNo.Trim());
                        contactCmd.Parameters.AddWithValue("@AlternateMobileNo", DbStr(c.AlternateMobileNo));
                        contactCmd.Parameters.AddWithValue("@Email", c.Email.Trim());
                        contactCmd.Parameters.AddWithValue("@IsPrimary", isPrimary);
                        contactCmd.Parameters.AddWithValue("@IsActive", NormalizeYN(c.IsActive, "Y"));
                        contactCmd.Parameters.AddWithValue("@CreatedBy", actorUserId);

                        await contactCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // =========================================================================
                // 3. SAVE TO dbo.Client_Branch (1 client : many branches)
                // =========================================================================
                if (!isNew)
                {
                    using var delBranchesCmd = new SqlCommand("DELETE FROM dbo.Client_Branch WHERE Client_Id = @ClientId;", conn, transaction);
                    delBranchesCmd.Parameters.AddWithValue("@ClientId", clientId);
                    await delBranchesCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                if (dto.Branches != null && dto.Branches.Count > 0)
                {
                    bool hoAssigned = false;

                    const string insertBranchSql = @"
                        INSERT INTO dbo.Client_Branch (
                            Client_Id, Branch_Code, Branch_Name, Branch_Type, GSTIN,
                            Address, Country, State, City, PIN_Code,
                            Contact_Person, Mobile_No, Email, Is_Head_Office, Branch_Status,
                            Created_By, Created_Date
                        )
                        VALUES (
                            @ClientId, @BranchCode, @BranchName, @BranchType, @Gstin,
                            @Address, @Country, @State, @City, @PinCode,
                            @ContactPerson, @MobileNo, @Email, @IsHeadOffice, @BranchStatus,
                            @CreatedBy, GETDATE()
                        );";

                    for (int i = 0; i < dto.Branches.Count; i++)
                    {
                        var b = dto.Branches[i];
                        string branchCode = string.IsNullOrWhiteSpace(b.BranchCode)
                            ? $"BR-{(i + 1):D2}"
                            : b.BranchCode.Trim();

                        bool wantsHo = NormalizeYN(b.IsHeadOffice, "N") == "Y";
                        string isHo = "N";
                        if (wantsHo && !hoAssigned)
                        {
                            isHo = "Y";
                            hoAssigned = true;
                        }
                        else if (i == 0 && !dto.Branches.Any(x => NormalizeYN(x.IsHeadOffice, "N") == "Y"))
                        {
                            isHo = "Y";
                            hoAssigned = true;
                        }

                        using var branchCmd = new SqlCommand(insertBranchSql, conn, transaction);
                        branchCmd.Parameters.AddWithValue("@ClientId", clientId);
                        branchCmd.Parameters.AddWithValue("@BranchCode", branchCode);
                        branchCmd.Parameters.AddWithValue("@BranchName", b.BranchName.Trim());
                        branchCmd.Parameters.AddWithValue("@BranchType", string.IsNullOrWhiteSpace(b.BranchType) ? "Head Office" : b.BranchType.Trim());
                        branchCmd.Parameters.AddWithValue("@Gstin", DbStr(b.Gstin));
                        branchCmd.Parameters.AddWithValue("@Address", b.Address.Trim());
                        branchCmd.Parameters.AddWithValue("@Country", string.IsNullOrWhiteSpace(b.Country) ? "India" : b.Country.Trim());
                        branchCmd.Parameters.AddWithValue("@State", b.State.Trim());
                        branchCmd.Parameters.AddWithValue("@City", b.City.Trim());
                        branchCmd.Parameters.AddWithValue("@PinCode", DbStr(b.PinCode));
                        branchCmd.Parameters.AddWithValue("@ContactPerson", DbStr(b.ContactPerson));
                        branchCmd.Parameters.AddWithValue("@MobileNo", DbStr(b.MobileNo));
                        branchCmd.Parameters.AddWithValue("@Email", DbStr(b.Email));
                        branchCmd.Parameters.AddWithValue("@IsHeadOffice", isHo);
                        branchCmd.Parameters.AddWithValue("@BranchStatus", string.IsNullOrWhiteSpace(b.BranchStatus) ? "Active" : b.BranchStatus.Trim());
                        branchCmd.Parameters.AddWithValue("@CreatedBy", actorUserId);

                        await branchCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // =========================================================================
                // 4. SAVE TO dbo.Client_Commercial (1 : 1 with client)
                // =========================================================================
                if (dto.Commercial != null)
                {
                    var com = dto.Commercial;
                    string billingEntity = string.IsNullOrWhiteSpace(com.BillingEntity)
                        ? dto.ClientName.Trim()
                        : com.BillingEntity.Trim();
                    string paymentTerms = string.IsNullOrWhiteSpace(com.PaymentTerms)
                        ? "Net 30"
                        : com.PaymentTerms.Trim();

                    string accountCode = (com.AccountCode ?? string.Empty).Trim();
                    if (string.IsNullOrEmpty(accountCode) || accountCode.StartsWith("Assigned", StringComparison.OrdinalIgnoreCase))
                    {
                        var prefix = new string(dto.ClientName.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
                        if (string.IsNullOrEmpty(prefix)) prefix = "CUS";
                        accountCode = $"AR-{prefix}-{clientId:D4}";
                    }

                    const string upsertCommercialSql = @"
                        IF EXISTS (SELECT 1 FROM dbo.Client_Commercial WHERE Client_Id = @ClientId)
                        BEGIN
                            UPDATE dbo.Client_Commercial
                            SET Account_Code           = @AccountCode,
                                Billing_Entity         = @BillingEntity,
                                Billing_GSTIN          = @BillingGstin,
                                Billing_Contact_Person = @BillingContactPerson,
                                Billing_Email          = @BillingEmail,
                                Billing_Address        = @BillingAddress,
                                Payment_Terms          = @PaymentTerms,
                                Credit_Period_Days     = @CreditPeriodDays,
                                Credit_Limit           = @CreditLimit,
                                Currency               = @Currency,
                                Invoice_Frequency      = @InvoiceFrequency,
                                Contract_Start_Date    = @ContractStartDate,
                                Contract_End_Date      = @ContractEndDate,
                                Agreement_Ref_No       = @AgreementRefNo,
                                Customer_PO_Ref        = @CustomerPoRef,
                                TDS_Applicable         = @TdsApplicable,
                                GST_Applicable         = @GstApplicable,
                                Tax_Category           = @TaxCategory,
                                Accounts_Remarks       = @AccountsRemarks,
                                Modified_By            = @CreatedBy,
                                Modified_Date          = GETDATE()
                            WHERE Client_Id = @ClientId;
                        END
                        ELSE
                        BEGIN
                            INSERT INTO dbo.Client_Commercial (
                                Client_Id, Account_Code, Billing_Entity, Billing_GSTIN,
                                Billing_Contact_Person, Billing_Email, Billing_Address,
                                Payment_Terms, Credit_Period_Days, Credit_Limit, Currency, Invoice_Frequency,
                                Contract_Start_Date, Contract_End_Date, Agreement_Ref_No, Customer_PO_Ref,
                                TDS_Applicable, GST_Applicable, Tax_Category, Accounts_Remarks,
                                Created_By, Created_Date
                            )
                            VALUES (
                                @ClientId, @AccountCode, @BillingEntity, @BillingGstin,
                                @BillingContactPerson, @BillingEmail, @BillingAddress,
                                @PaymentTerms, @CreditPeriodDays, @CreditLimit, @Currency, @InvoiceFrequency,
                                @ContractStartDate, @ContractEndDate, @AgreementRefNo, @CustomerPoRef,
                                @TdsApplicable, @GstApplicable, @TaxCategory, @AccountsRemarks,
                                @CreatedBy, GETDATE()
                            );
                        END";

                    using var comCmd = new SqlCommand(upsertCommercialSql, conn, transaction);
                    comCmd.Parameters.AddWithValue("@ClientId", clientId);
                    comCmd.Parameters.AddWithValue("@AccountCode", accountCode);
                    comCmd.Parameters.AddWithValue("@BillingEntity", billingEntity);
                    comCmd.Parameters.AddWithValue("@BillingGstin", DbStr(com.BillingGstin));
                    comCmd.Parameters.AddWithValue("@BillingContactPerson", DbStr(com.BillingContactPerson));
                    comCmd.Parameters.AddWithValue("@BillingEmail", DbStr(com.BillingEmail));
                    comCmd.Parameters.AddWithValue("@BillingAddress", DbStr(com.BillingAddress));
                    comCmd.Parameters.AddWithValue("@PaymentTerms", paymentTerms);
                    comCmd.Parameters.AddWithValue("@CreditPeriodDays", DbVal(com.CreditPeriodDays));
                    comCmd.Parameters.AddWithValue("@CreditLimit", DbVal(com.CreditLimit));
                    comCmd.Parameters.AddWithValue("@Currency", string.IsNullOrWhiteSpace(com.Currency) ? "INR" : com.Currency.Trim().ToUpperInvariant());
                    comCmd.Parameters.AddWithValue("@InvoiceFrequency", DbStr(com.InvoiceFrequency));
                    comCmd.Parameters.AddWithValue("@ContractStartDate", DbVal(com.ContractStartDate));
                    comCmd.Parameters.AddWithValue("@ContractEndDate", DbVal(com.ContractEndDate));
                    comCmd.Parameters.AddWithValue("@AgreementRefNo", DbStr(com.AgreementRefNo));
                    comCmd.Parameters.AddWithValue("@CustomerPoRef", DbStr(com.CustomerPoRef));
                    comCmd.Parameters.AddWithValue("@TdsApplicable", DbVal(NormalizeNullableYN(com.TdsApplicable)));
                    comCmd.Parameters.AddWithValue("@GstApplicable", DbVal(NormalizeNullableYN(com.GstApplicable)));
                    comCmd.Parameters.AddWithValue("@TaxCategory", DbStr(com.TaxCategory));
                    comCmd.Parameters.AddWithValue("@AccountsRemarks", DbStr(com.AccountsRemarks));
                    comCmd.Parameters.AddWithValue("@CreatedBy", actorUserId);

                    await comCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // =========================================================================
                // 5. SAVE TO dbo.Client_Technical_Config (1 : many, per Product_Code)
                // =========================================================================
                if (dto.TechnicalConfigs != null && dto.TechnicalConfigs.Any())
                {
                    const string upsertTechSql = @"
                        IF EXISTS (SELECT 1 FROM dbo.Client_Technical_Config WHERE Client_Id = @ClientId AND Product_Code = @ProductCode)
                        BEGIN
                            UPDATE dbo.Client_Technical_Config
                            SET Deployment_Type   = @DeploymentType,
                                SQL_Version       = @SqlVersion,
                                Server_Host       = @ServerHost,
                                Port              = @Port,
                                Database_Name     = @DatabaseName,
                                DB_User           = @DbUser,
                                DB_Password       = @DbPassword,
                                Connection_String = @ConnectionString,
                                Published_App_Url = @PublishedAppUrl,
                                Api_Url           = @ApiUrl,
                                Is_Active         = @IsActive,
                                Technical_Remarks = @TechnicalRemarks,
                                Modified_By       = @CreatedBy,
                                Modified_Date     = GETDATE()
                            WHERE Client_Id = @ClientId AND Product_Code = @ProductCode;
                        END
                        ELSE
                        BEGIN
                            INSERT INTO dbo.Client_Technical_Config (
                                Client_Id, Product_Code, Deployment_Type, SQL_Version,
                                Server_Host, Port, Database_Name, DB_User, DB_Password,
                                Connection_String, Published_App_Url, Api_Url,
                                Is_Active, Technical_Remarks, Created_By, Created_Date
                            )
                            VALUES (
                                @ClientId, @ProductCode, @DeploymentType, @SqlVersion,
                                @ServerHost, @Port, @DatabaseName, @DbUser, @DbPassword,
                                @ConnectionString, @PublishedAppUrl, @ApiUrl,
                                @IsActive, @TechnicalRemarks, @CreatedBy, GETDATE()
                            );
                        END";

                    foreach (var tc in dto.TechnicalConfigs)
                    {
                        if (string.IsNullOrWhiteSpace(tc.ProductCode)) continue;

                        string prodCode = tc.ProductCode.Trim().ToUpperInvariant();
                        string deploy = string.IsNullOrWhiteSpace(tc.DeploymentType) ? "Cloud" : tc.DeploymentType.Trim();
                        // Constraint CK_Client_Tech_Deploy CHECK (Deployment_Type IN ('Cloud', 'Standalone'))
                        if (deploy != "Standalone") deploy = "Cloud";

                        string techActive = NormalizeYN(tc.IsActive, "Y");
                        string dbName = string.IsNullOrWhiteSpace(tc.DatabaseName) ? $"DB_{customerCode}_{prodCode}" : tc.DatabaseName.Trim();

                        using var techCmd = new SqlCommand(upsertTechSql, conn, transaction);
                        techCmd.Parameters.AddWithValue("@ClientId", clientId);
                        techCmd.Parameters.AddWithValue("@ProductCode", prodCode);
                        techCmd.Parameters.AddWithValue("@DeploymentType", deploy);
                        techCmd.Parameters.AddWithValue("@SqlVersion", DbStr(tc.SqlVersion) ?? (object)"SQL Server 2022");
                        techCmd.Parameters.AddWithValue("@ServerHost", DbStr(tc.ServerHost));
                        techCmd.Parameters.AddWithValue("@Port", DbVal(tc.Port ?? 1433));
                        techCmd.Parameters.AddWithValue("@DatabaseName", dbName);
                        techCmd.Parameters.AddWithValue("@DbUser", DbStr(tc.DbUser));
                        techCmd.Parameters.AddWithValue("@DbPassword", DbStr(tc.DbPassword));
                        techCmd.Parameters.AddWithValue("@ConnectionString", DbStr(tc.ConnectionString));
                        techCmd.Parameters.AddWithValue("@PublishedAppUrl", DbStr(tc.PublishedAppUrl));
                        techCmd.Parameters.AddWithValue("@ApiUrl", DbStr(tc.ApiUrl));
                        techCmd.Parameters.AddWithValue("@IsActive", techActive);
                        techCmd.Parameters.AddWithValue("@TechnicalRemarks", DbStr(tc.TechnicalRemarks));
                        techCmd.Parameters.AddWithValue("@CreatedBy", actorUserId);

                        await techCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // =========================================================================
                // 6. SAVE TO dbo.Client_Subscription (1 : many, per Product_Code)
                // =========================================================================
                if (dto.Subscriptions != null && dto.Subscriptions.Any())
                {
                    const string upsertSubSql = @"
                        IF EXISTS (SELECT 1 FROM dbo.Client_Subscription WHERE Client_Id = @ClientId AND Product_Code = @ProductCode)
                        BEGIN
                            UPDATE dbo.Client_Subscription
                            SET Subscription_Type    = @SubscriptionType,
                                Edition              = @Edition,
                                License_Type         = @LicenseType,
                                Licensed_Users       = @LicensedUsers,
                                Active_Users         = @ActiveUsers,
                                Start_Date           = @StartDate,
                                End_Date             = @EndDate,
                                Renewal_Date         = @RenewalDate,
                                Subscription_Status  = @SubscriptionStatus,
                                Branch_Limit         = @BranchLimit,
                                Db_Limit             = @DbLimit,
                                Tech_Owner           = @TechOwner,
                                Commercial_Owner     = @CommercialOwner,
                                Subscription_Remarks = @SubscriptionRemarks,
                                Is_Active            = @IsActive,
                                Modified_By          = @CreatedBy,
                                Modified_Date        = GETDATE()
                            WHERE Client_Id = @ClientId AND Product_Code = @ProductCode;
                        END
                        ELSE
                        BEGIN
                            INSERT INTO dbo.Client_Subscription (
                                Client_Id, Product_Code, Subscription_Type, Edition,
                                License_Type, Licensed_Users, Active_Users,
                                Start_Date, End_Date, Renewal_Date,
                                Subscription_Status, Branch_Limit, Db_Limit,
                                Tech_Owner, Commercial_Owner, Subscription_Remarks,
                                Is_Active, Created_By, Created_Date
                            )
                            VALUES (
                                @ClientId, @ProductCode, @SubscriptionType, @Edition,
                                @LicenseType, @LicensedUsers, @ActiveUsers,
                                @StartDate, @EndDate, @RenewalDate,
                                @SubscriptionStatus, @BranchLimit, @DbLimit,
                                @TechOwner, @CommercialOwner, @SubscriptionRemarks,
                                @IsActive, @CreatedBy, GETDATE()
                            );
                        END";

                    foreach (var s in dto.Subscriptions)
                    {
                        if (string.IsNullOrWhiteSpace(s.ProductCode)) continue;

                        string prodCode = s.ProductCode.Trim().ToUpperInvariant();
                        string subType = string.IsNullOrWhiteSpace(s.SubscriptionType) ? "Annual" : s.SubscriptionType.Trim();
                        string edition = string.IsNullOrWhiteSpace(s.Edition) ? "Standard" : s.Edition.Trim();
                        string subStatus = string.IsNullOrWhiteSpace(s.SubscriptionStatus) ? "Pending Approval" : s.SubscriptionStatus.Trim();
                        string subActive = NormalizeYN(s.IsActive, "Y");

                        using var subCmd = new SqlCommand(upsertSubSql, conn, transaction);
                        subCmd.Parameters.AddWithValue("@ClientId", clientId);
                        subCmd.Parameters.AddWithValue("@ProductCode", prodCode);
                        subCmd.Parameters.AddWithValue("@SubscriptionType", subType);
                        subCmd.Parameters.AddWithValue("@Edition", edition);
                        subCmd.Parameters.AddWithValue("@LicenseType", DbStr(s.LicenseType) ?? (object)"Named user");
                        subCmd.Parameters.AddWithValue("@LicensedUsers", s.LicensedUsers > 0 ? s.LicensedUsers : 1);
                        subCmd.Parameters.AddWithValue("@ActiveUsers", s.ActiveUsers >= 0 ? s.ActiveUsers : 0);
                        subCmd.Parameters.AddWithValue("@StartDate", DbVal(s.StartDate ?? DateTime.Today));
                        subCmd.Parameters.AddWithValue("@EndDate", DbVal(s.EndDate));
                        subCmd.Parameters.AddWithValue("@RenewalDate", DbVal(s.RenewalDate));
                        subCmd.Parameters.AddWithValue("@SubscriptionStatus", subStatus);
                        subCmd.Parameters.AddWithValue("@BranchLimit", DbVal(s.BranchLimit ?? 1));
                        subCmd.Parameters.AddWithValue("@DbLimit", DbVal(s.DbLimit ?? 1));
                        subCmd.Parameters.AddWithValue("@TechOwner", DbStr(s.TechOwner));
                        subCmd.Parameters.AddWithValue("@CommercialOwner", DbStr(s.CommercialOwner));
                        subCmd.Parameters.AddWithValue("@SubscriptionRemarks", DbStr(s.SubscriptionRemarks));
                        subCmd.Parameters.AddWithValue("@IsActive", subActive);
                        subCmd.Parameters.AddWithValue("@CreatedBy", actorUserId);

                        await subCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // =========================================================================
                // 7. SAVE TO dbo.Client_Product_Activation (Initializes or updates Activation per product)
                // =========================================================================
                if (dto.Subscriptions != null && dto.Subscriptions.Any())
                {
                    const string initActSql = @"
                        IF NOT EXISTS (SELECT 1 FROM dbo.Client_Product_Activation WHERE Client_Id = @ClientId AND Product_Code = @ProductCode)
                        BEGIN
                            INSERT INTO dbo.Client_Product_Activation (
                                Client_Id, Product_Code, Current_Step_No, Activation_Status,
                                Is_On_Hold, Tech_Owner, Created_By, Created_Date
                            )
                            VALUES (
                                @ClientId, @ProductCode, 1, 'In Progress',
                                'N', @TechOwner, @CreatedBy, GETDATE()
                            );

                            DECLARE @NewActId int = SCOPE_IDENTITY();
                            INSERT INTO dbo.Client_Activation_Step_History (
                                Activation_Id, Step_No, Step_Name, Status,
                                Completed_Date, Completed_By_User, User_Role, Remarks
                            )
                            VALUES (
                                @NewActId, 1, 'Customer Registration', 'Completed',
                                GETDATE(), 'System', 'Admin', 'Customer registered and initial activation tracker created'
                            );
                        END;";

                    foreach (var s in dto.Subscriptions)
                    {
                        if (string.IsNullOrWhiteSpace(s.ProductCode)) continue;
                        string prodCode = s.ProductCode.Trim().ToUpperInvariant();

                        using var initActCmd = new SqlCommand(initActSql, conn, transaction);
                        initActCmd.Parameters.AddWithValue("@ClientId", clientId);
                        initActCmd.Parameters.AddWithValue("@ProductCode", prodCode);
                        initActCmd.Parameters.AddWithValue("@TechOwner", DbStr(s.TechOwner));
                        initActCmd.Parameters.AddWithValue("@CreatedBy", actorUserId);

                        await initActCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // =========================================================================
                // 7. SAVE TO dbo.Client_Technical_History (Version Audit History)
                // =========================================================================
                if (dto.TechnicalHistories != null && dto.TechnicalHistories.Any())
                {
                    const string insertHistSql = @"
                        IF OBJECT_ID('dbo.Client_Technical_History', 'U') IS NOT NULL
                        BEGIN
                            IF NOT EXISTS (SELECT 1 FROM dbo.Client_Technical_History 
                                           WHERE Client_Id = @ClientId 
                                             AND Product_Code = @ProductCode 
                                             AND Version_Label = @VersionLabel 
                                             AND Field_Name = @FieldName)
                            BEGIN
                                INSERT INTO dbo.Client_Technical_History (
                                    Client_Id, Product_Code, Version_Label, Field_Name,
                                    Previous_Value, New_Value, Change_Reason, Created_By, Created_Date
                                )
                                VALUES (
                                    @ClientId, @ProductCode, @VersionLabel, @FieldName,
                                    @PreviousValue, @NewValue, @ChangeReason, @CreatedBy, @CreatedDate
                                );
                            END
                        END";

                    foreach (var h in dto.TechnicalHistories)
                    {
                        if (string.IsNullOrWhiteSpace(h.FieldName)) continue;
                        using var hCmd = new SqlCommand(insertHistSql, conn, transaction);
                        hCmd.Parameters.AddWithValue("@ClientId", clientId);
                        hCmd.Parameters.AddWithValue("@ProductCode", string.IsNullOrWhiteSpace(h.ProductCode) ? "ERP" : h.ProductCode.Trim().ToUpperInvariant());
                        hCmd.Parameters.AddWithValue("@VersionLabel", string.IsNullOrWhiteSpace(h.VersionLabel) ? "v1" : h.VersionLabel.Trim());
                        hCmd.Parameters.AddWithValue("@FieldName", h.FieldName.Trim());
                        hCmd.Parameters.AddWithValue("@PreviousValue", DbStr(h.PreviousValue));
                        hCmd.Parameters.AddWithValue("@NewValue", DbStr(h.NewValue));
                        hCmd.Parameters.AddWithValue("@ChangeReason", DbStr(h.ChangeReason));
                        hCmd.Parameters.AddWithValue("@CreatedBy", DbStr(h.CreatedBy) ?? (object)actorUserId);
                        hCmd.Parameters.AddWithValue("@CreatedDate", h.CreatedDate ?? DateTime.Now);

                        await hCmd.ExecuteNonQueryAsync(cancellationToken);
                    }
                }

                // =========================================================================
                // 8. SAVE TO dbo.Client_Audit_Log
                // =========================================================================
                if (dto.AuditLogs != null && dto.AuditLogs.Any())
                {
                    const string insertAuditSql = @"
                        IF OBJECT_ID('dbo.Client_Audit_Log', 'U') IS NOT NULL
                        BEGIN
                            INSERT INTO dbo.Client_Audit_Log (
                                Client_Id, Section_Name, Table_Name, Record_Id,
                                Field_Name, Previous_Value, Updated_Value, Change_Reason,
                                Changed_By, Changed_Date
                            )
                            VALUES (
                                @ClientId, @SectionName, @TableName, @RecordId,
                                @FieldName, @PreviousValue, @UpdatedValue, @ChangeReason,
                                @ChangedBy, @ChangedDate
                            );
                        END";

                    try
                    {
                        foreach (var a in dto.AuditLogs)
                        {
                            if (a.AuditId.HasValue && a.AuditId.Value > 0) continue;
                            if (string.IsNullOrWhiteSpace(a.FieldName)) continue;
                            using var aCmd = new SqlCommand(insertAuditSql, conn, transaction);
                            aCmd.Parameters.AddWithValue("@ClientId", clientId);
                            aCmd.Parameters.AddWithValue("@SectionName", string.IsNullOrWhiteSpace(a.SectionName) ? "Customer Profile" : a.SectionName.Trim());
                            aCmd.Parameters.AddWithValue("@TableName", string.IsNullOrWhiteSpace(a.TableName) ? "Client_Master" : a.TableName.Trim());
                            aCmd.Parameters.AddWithValue("@RecordId", DbVal(a.RecordId));
                            aCmd.Parameters.AddWithValue("@FieldName", a.FieldName.Trim());
                            aCmd.Parameters.AddWithValue("@PreviousValue", DbStr(a.PreviousValue));
                            aCmd.Parameters.AddWithValue("@UpdatedValue", DbStr(a.UpdatedValue));
                            aCmd.Parameters.AddWithValue("@ChangeReason", DbStr(a.ChangeReason));
                            aCmd.Parameters.AddWithValue("@ChangedBy", a.ChangedBy > 0 ? a.ChangedBy : actorUserId);
                            aCmd.Parameters.AddWithValue("@ChangedDate", a.ChangedDate ?? DateTime.Now);

                            await aCmd.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }
                    catch (SqlException ex)
                    {
                        _logger.LogWarning(ex, "Could not save Client_Audit_Log (table may not exist yet in database).");
                    }
                }

                // =========================================================================
                // 9. COMMIT TRANSACTION (All tables succeeded)
                // =========================================================================
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation("Successfully saved Client Registration for Client_Id={ClientId}, Customer_Code={CustomerCode}", clientId, customerCode);

                var saved = await GetClientRegistrationByIdAsync(clientId, cancellationToken);
                return saved ?? new ClientRegistrationResponseDto
                {
                    ClientId = clientId,
                    CustomerCode = customerCode,
                    ClientName = dto.ClientName
                };
            }
            catch (Exception ex)
            {
                // Rollback all changes across Client_Master, Client_Contact, Client_Branch, and Client_Commercial
                try
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                catch (Exception rbEx)
                {
                    _logger.LogError(rbEx, "Error rolling back transaction during Client Registration");
                }

                _logger.LogError(ex, "Transaction rolled back for Client Registration ({ClientName})", dto.ClientName);
                throw;
            }
        }

        private static void BindMasterParameters(
            SqlCommand cmd,
            ClientRegistrationRequestDto dto,
            string customerCode,
            string isActive,
            string stage,
            bool isAdminSubmitted,
            int actorUserId)
        {
            cmd.Parameters.AddWithValue("@ClientName", dto.ClientName.Trim());
            cmd.Parameters.AddWithValue("@ClientAddress", DbStr(dto.ClientAddress));
            cmd.Parameters.AddWithValue("@IsActive", isActive);
            cmd.Parameters.AddWithValue("@CustomerCode", DbStr(customerCode));
            cmd.Parameters.AddWithValue("@DisplayName", DbStr(dto.DisplayName));
            cmd.Parameters.AddWithValue("@CustomerType", DbStr(dto.CustomerType));
            cmd.Parameters.AddWithValue("@Website", DbStr(dto.Website));
            cmd.Parameters.AddWithValue("@YearOfEstablishment", DbVal(dto.YearOfEstablishment));
            cmd.Parameters.AddWithValue("@IndustryCategory", DbStr(dto.IndustryCategory));
            cmd.Parameters.AddWithValue("@BusinessSegment", DbStr(dto.BusinessSegment));
            cmd.Parameters.AddWithValue("@CompanySize", DbStr(dto.CompanySize));
            cmd.Parameters.AddWithValue("@NumberOfBranches", DbVal(dto.NumberOfBranches ?? dto.Branches?.Count));
            cmd.Parameters.AddWithValue("@CustomerSource", DbStr(dto.CustomerSource));
            cmd.Parameters.AddWithValue("@RelationshipManagerId", DbVal(dto.RelationshipManagerId));
            // Enforce exact CK_Client_Master_Status values: ('Prospect', 'Onboarding', 'Active', 'Suspended', 'Inactive')
            string rawStatus = dto.CustomerStatus?.Trim() ?? string.Empty;
            string status;
            if (string.Equals(rawStatus, "Incomplete", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawStatus, "Onboarding", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rawStatus, "Pending Tech", StringComparison.OrdinalIgnoreCase))
            {
                status = "Onboarding";
            }
            else if (string.Equals(rawStatus, "Active", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(rawStatus, "Registered", StringComparison.OrdinalIgnoreCase))
            {
                status = "Active";
            }
            else if (string.Equals(rawStatus, "Suspended", StringComparison.OrdinalIgnoreCase))
            {
                status = "Suspended";
            }
            else if (string.Equals(rawStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                status = "Inactive";
            }
            else
            {
                status = stage == "COMPLETED" ? "Active" : (stage == "PENDING_TECH" ? "Onboarding" : "Prospect");
            }

            cmd.Parameters.AddWithValue("@CustomerStatus", status);
            cmd.Parameters.AddWithValue("@RegistrationDate", dto.RegistrationDate ?? DateTime.Today);
            cmd.Parameters.AddWithValue("@InternalRemarks", DbStr(dto.InternalRemarks));
            cmd.Parameters.AddWithValue("@PanNo", DbStr(dto.PanNo));
            cmd.Parameters.AddWithValue("@GstRegistrationType", DbStr(dto.GstRegistrationType));
            cmd.Parameters.AddWithValue("@GstinPrimary", DbStr(dto.GstinPrimary));
            cmd.Parameters.AddWithValue("@CinNo", DbStr(dto.CinNo));
            cmd.Parameters.AddWithValue("@LlpinNo", DbStr(dto.LlpinNo));
            cmd.Parameters.AddWithValue("@TanNo", DbStr(dto.TanNo));
            cmd.Parameters.AddWithValue("@MsmeRegNo", DbStr(dto.MsmeRegNo));
            cmd.Parameters.AddWithValue("@IecNo", DbStr(dto.IecNo));
            cmd.Parameters.AddWithValue("@BillingAddress", DbStr(dto.BillingAddress));
            cmd.Parameters.AddWithValue("@Country", string.IsNullOrWhiteSpace(dto.Country) ? "India" : dto.Country.Trim());
            cmd.Parameters.AddWithValue("@State", DbStr(dto.State));
            cmd.Parameters.AddWithValue("@City", DbStr(dto.City));
            cmd.Parameters.AddWithValue("@District", DbStr(dto.District));
            cmd.Parameters.AddWithValue("@PinCode", DbStr(dto.PinCode));
            cmd.Parameters.AddWithValue("@RegistrationStage", stage);
            cmd.Parameters.AddWithValue("@AdminSubmittedBy", isAdminSubmitted ? actorUserId : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@AdminSubmittedDate", isAdminSubmitted ? DateTime.Now : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@CreatedBy", actorUserId);
        }

        public async Task<ClientRegistrationResponseDto?> GetClientRegistrationByIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            ClientRegistrationResponseDto? result = null;

            const string masterSql = @"
               SELECT m.Client_Id, m.Client_Name, m.Client_Address, m.Is_Active,
                       m.Customer_Code, m.Display_Name, m.Customer_Type, m.Website, m.Year_Of_Establishment,
                       m.Industry_Category, m.Business_Segment, m.Company_Size, m.Number_Of_Branches,
                       m.Customer_Source, m.Relationship_Manager_Id, m.Customer_Status, m.Registration_Date, m.Internal_Remarks,
                       m.PAN_No, m.GST_Registration_Type, m.GSTIN_Primary, m.CIN_No, m.LLPIN_No, m.TAN_No, m.MSME_Reg_No, m.IEC_No,
                       m.Billing_Address, m.Country, m.State, m.City, m.District, m.PIN_Code, m.Registration_Stage,
                       ISNULL(NULLIF(LTRIM(RTRIM(cu.FullName)), ''), cu.Username) AS CreatedByName,
                       ISNULL(NULLIF(LTRIM(RTRIM(tu.FullName)), ''), tu.Username) AS TechnicalSignOffByName
                FROM dbo.Client_Master m
                LEFT JOIN dbo.UserMaster cu ON COALESCE(m.Admin_Submitted_By, m.Created_By) = cu.UserId
                LEFT JOIN dbo.UserMaster tu ON m.Tech_Submitted_By = tu.UserId
                WHERE m.Client_Id = @ClientId;";

            using (var cmd = new SqlCommand(masterSql, conn))
            {
                cmd.Parameters.AddWithValue("@ClientId", clientId);
                using var r = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await r.ReadAsync(cancellationToken))
                {
                    result = MapMasterRow(r);
                    result.CreatedByName = r["CreatedByName"] as string;
                    result.TechnicalSignOffByName = r["TechnicalSignOffByName"] as string;
                }
            }

            if (result == null) return null;

            // Load Contacts
            const string contactsSql = @"
                SELECT Contact_Id, Contact_Name, Designation, Department, Contact_Category,
                       Mobile_No, Alternate_Mobile_No, Email, Is_Primary, Is_Active
                FROM dbo.Client_Contact
                WHERE Client_Id = @ClientId
                ORDER BY CASE WHEN Is_Primary = 'Y' THEN 0 ELSE 1 END, Contact_Id ASC;";

            using (var cCmd = new SqlCommand(contactsSql, conn))
            {
                cCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var cr = await cCmd.ExecuteReaderAsync(cancellationToken);
                while (await cr.ReadAsync(cancellationToken))
                {
                    result.Contacts.Add(new ClientContactDto
                    {
                        ContactId = Convert.ToInt32(cr["Contact_Id"]),
                        ContactName = cr["Contact_Name"]?.ToString() ?? string.Empty,
                        Designation = cr["Designation"] as string,
                        Department = cr["Department"] as string,
                        ContactCategory = cr["Contact_Category"]?.ToString() ?? "Primary Contact",
                        MobileNo = cr["Mobile_No"]?.ToString() ?? string.Empty,
                        AlternateMobileNo = cr["Alternate_Mobile_No"] as string,
                        Email = cr["Email"]?.ToString() ?? string.Empty,
                        IsPrimary = cr["Is_Primary"]?.ToString()?.Trim() ?? "N",
                        IsActive = cr["Is_Active"]?.ToString()?.Trim() ?? "Y"
                    });
                }
            }

            // Load Branches
            const string branchesSql = @"
                SELECT Branch_Id, Branch_Code, Branch_Name, Branch_Type, GSTIN,
                       Address, Country, State, City, PIN_Code,
                       Contact_Person, Mobile_No, Email, Is_Head_Office, Branch_Status
                FROM dbo.Client_Branch
                WHERE Client_Id = @ClientId
                ORDER BY CASE WHEN Is_Head_Office = 'Y' THEN 0 ELSE 1 END, Branch_Id ASC;";

            using (var bCmd = new SqlCommand(branchesSql, conn))
            {
                bCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var br = await bCmd.ExecuteReaderAsync(cancellationToken);
                while (await br.ReadAsync(cancellationToken))
                {
                    result.Branches.Add(new ClientBranchDto
                    {
                        BranchId = Convert.ToInt32(br["Branch_Id"]),
                        BranchCode = br["Branch_Code"]?.ToString(),
                        BranchName = br["Branch_Name"]?.ToString() ?? string.Empty,
                        BranchType = br["Branch_Type"]?.ToString() ?? "Head Office",
                        Gstin = br["GSTIN"]?.ToString()?.Trim(),
                        Address = br["Address"]?.ToString() ?? string.Empty,
                        Country = br["Country"] as string ?? "India",
                        State = br["State"]?.ToString() ?? string.Empty,
                        City = br["City"]?.ToString() ?? string.Empty,
                        PinCode = br["PIN_Code"]?.ToString()?.Trim(),
                        ContactPerson = br["Contact_Person"] as string,
                        MobileNo = br["Mobile_No"] as string,
                        Email = br["Email"] as string,
                        IsHeadOffice = br["Is_Head_Office"]?.ToString()?.Trim() ?? "N",
                        BranchStatus = br["Branch_Status"]?.ToString() ?? "Active"
                    });
                }
            }

            // Load Commercial
            const string comSql = @"
                SELECT Account_Code, Billing_Entity, Billing_GSTIN, Billing_Contact_Person,
                       Billing_Email, Billing_Address, Payment_Terms, Credit_Period_Days,
                       Credit_Limit, Currency, Invoice_Frequency, Contract_Start_Date,
                       Contract_End_Date, Agreement_Ref_No, Customer_PO_Ref,
                       TDS_Applicable, GST_Applicable, Tax_Category, Accounts_Remarks
                FROM dbo.Client_Commercial
                WHERE Client_Id = @ClientId;";

            using (var comCmd = new SqlCommand(comSql, conn))
            {
                comCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var mr = await comCmd.ExecuteReaderAsync(cancellationToken);
                if (await mr.ReadAsync(cancellationToken))
                {
                    result.Commercial = new ClientCommercialDto
                    {
                        AccountCode = mr["Account_Code"] as string,
                        BillingEntity = mr["Billing_Entity"] as string,
                        BillingGstin = mr["Billing_GSTIN"]?.ToString()?.Trim(),
                        BillingContactPerson = mr["Billing_Contact_Person"] as string,
                        BillingEmail = mr["Billing_Email"] as string,
                        BillingAddress = mr["Billing_Address"] as string,
                        PaymentTerms = mr["Payment_Terms"] as string,
                        CreditPeriodDays = mr["Credit_Period_Days"] == DBNull.Value ? null : Convert.ToInt32(mr["Credit_Period_Days"]),
                        CreditLimit = mr["Credit_Limit"] == DBNull.Value ? null : Convert.ToDecimal(mr["Credit_Limit"]),
                        Currency = mr["Currency"]?.ToString()?.Trim() ?? "INR",
                        InvoiceFrequency = mr["Invoice_Frequency"] as string,
                        ContractStartDate = mr["Contract_Start_Date"] == DBNull.Value ? null : Convert.ToDateTime(mr["Contract_Start_Date"]),
                        ContractEndDate = mr["Contract_End_Date"] == DBNull.Value ? null : Convert.ToDateTime(mr["Contract_End_Date"]),
                        AgreementRefNo = mr["Agreement_Ref_No"] as string,
                        CustomerPoRef = mr["Customer_PO_Ref"] as string,
                        TdsApplicable = mr["TDS_Applicable"]?.ToString()?.Trim(),
                        GstApplicable = mr["GST_Applicable"]?.ToString()?.Trim(),
                        TaxCategory = mr["Tax_Category"] as string,
                        AccountsRemarks = mr["Accounts_Remarks"] as string
                    };
                }
            }

            // Load Technical Configs (dbo.Client_Technical_Config)
            const string techSql = @"
                SELECT Config_Id, Product_Code, Deployment_Type, SQL_Version, Server_Host,
                       Port, Database_Name, DB_User, DB_Password, Connection_String,
                       Published_App_Url, Api_Url, Is_Active, Technical_Remarks
                FROM dbo.Client_Technical_Config
                WHERE Client_Id = @ClientId
                ORDER BY Config_Id ASC;";

            using (var techCmd = new SqlCommand(techSql, conn))
            {
                techCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var tr = await techCmd.ExecuteReaderAsync(cancellationToken);
                while (await tr.ReadAsync(cancellationToken))
                {
                    result.TechnicalConfigs.Add(new ClientTechnicalDto
                    {
                        ConfigId = Convert.ToInt32(tr["Config_Id"]),
                        ProductCode = tr["Product_Code"]?.ToString()?.Trim() ?? "ERP",
                        DeploymentType = tr["Deployment_Type"]?.ToString()?.Trim() ?? "Cloud",
                        SqlVersion = tr["SQL_Version"] as string ?? "SQL Server 2022",
                        ServerHost = tr["Server_Host"] as string,
                        Port = tr["Port"] == DBNull.Value ? 1433 : Convert.ToInt32(tr["Port"]),
                        DatabaseName = tr["Database_Name"]?.ToString() ?? string.Empty,
                        DbUser = tr["DB_User"] as string,
                        DbPassword = tr["DB_Password"] as string,
                        ConnectionString = tr["Connection_String"] as string,
                        PublishedAppUrl = tr["Published_App_Url"] as string,
                        ApiUrl = tr["Api_Url"] as string,
                        IsActive = tr["Is_Active"]?.ToString()?.Trim() ?? "Y",
                        TechnicalRemarks = tr["Technical_Remarks"] as string
                    });
                }
            }

            // Load Subscriptions (dbo.Client_Subscription)
            const string subSql = @"
                SELECT Subscription_Id, Product_Code, Subscription_Type, Edition,
                       License_Type, Licensed_Users, Active_Users, Start_Date,
                       End_Date, Renewal_Date, Subscription_Status, Branch_Limit,
                       Db_Limit, Tech_Owner, Commercial_Owner, Subscription_Remarks,
                       Is_Active
                FROM dbo.Client_Subscription
                WHERE Client_Id = @ClientId
                ORDER BY Subscription_Id ASC;";

            using (var subCmd = new SqlCommand(subSql, conn))
            {
                subCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var sr = await subCmd.ExecuteReaderAsync(cancellationToken);
                while (await sr.ReadAsync(cancellationToken))
                {
                    result.Subscriptions.Add(new ClientSubscriptionDto
                    {
                        SubscriptionId = Convert.ToInt32(sr["Subscription_Id"]),
                        ProductCode = sr["Product_Code"]?.ToString()?.Trim() ?? "ERP",
                        SubscriptionType = sr["Subscription_Type"]?.ToString()?.Trim() ?? "Annual",
                        Edition = sr["Edition"]?.ToString()?.Trim() ?? "Standard",
                        LicenseType = sr["License_Type"] as string ?? "Named user",
                        LicensedUsers = sr["Licensed_Users"] == DBNull.Value ? 1 : Convert.ToInt32(sr["Licensed_Users"]),
                        ActiveUsers = sr["Active_Users"] == DBNull.Value ? 0 : Convert.ToInt32(sr["Active_Users"]),
                        StartDate = sr["Start_Date"] == DBNull.Value ? null : Convert.ToDateTime(sr["Start_Date"]),
                        EndDate = sr["End_Date"] == DBNull.Value ? null : Convert.ToDateTime(sr["End_Date"]),
                        RenewalDate = sr["Renewal_Date"] == DBNull.Value ? null : Convert.ToDateTime(sr["Renewal_Date"]),
                        SubscriptionStatus = sr["Subscription_Status"]?.ToString()?.Trim() ?? "Pending Approval",
                        BranchLimit = sr["Branch_Limit"] == DBNull.Value ? null : Convert.ToInt32(sr["Branch_Limit"]),
                        DbLimit = sr["Db_Limit"] == DBNull.Value ? null : Convert.ToInt32(sr["Db_Limit"]),
                        TechOwner = sr["Tech_Owner"] as string,
                        CommercialOwner = sr["Commercial_Owner"] as string,
                        SubscriptionRemarks = sr["Subscription_Remarks"] as string,
                        IsActive = sr["Is_Active"]?.ToString()?.Trim() ?? "Y"
                    });
                }
            }

            // Load Activations & Step Histories (dbo.Client_Product_Activation)
            const string actSql = @"
                SELECT Activation_Id, Product_Code, Current_Step_No, Activation_Status,
                       Is_On_Hold, Hold_Reason, Tech_Owner, Tech_Done_Date, Go_Live_Date
                FROM dbo.Client_Product_Activation
                WHERE Client_Id = @ClientId
                ORDER BY Activation_Id ASC;";

            using (var actCmd = new SqlCommand(actSql, conn))
            {
                actCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var ar = await actCmd.ExecuteReaderAsync(cancellationToken);
                while (await ar.ReadAsync(cancellationToken))
                {
                    result.Activations.Add(new ClientActivationDto
                    {
                        ActivationId = Convert.ToInt32(ar["Activation_Id"]),
                        ProductCode = ar["Product_Code"]?.ToString()?.Trim() ?? "ERP",
                        CurrentStepNo = Convert.ToInt32(ar["Current_Step_No"]),
                        ActivationStatus = ar["Activation_Status"]?.ToString()?.Trim() ?? "In Progress",
                        IsOnHold = ar["Is_On_Hold"]?.ToString()?.Trim() ?? "N",
                        HoldReason = ar["Hold_Reason"] as string,
                        TechOwner = ar["Tech_Owner"] as string,
                        TechDoneDate = ar["Tech_Done_Date"] == DBNull.Value ? null : Convert.ToDateTime(ar["Tech_Done_Date"]),
                        GoLiveDate = ar["Go_Live_Date"] == DBNull.Value ? null : Convert.ToDateTime(ar["Go_Live_Date"])
                    });
                }
            }

            // For each activation, load step history
            foreach (var actItem in result.Activations)
            {
                if (actItem.ActivationId.HasValue)
                {
                    const string histSql = @"
                        SELECT Step_History_Id, Activation_Id, Step_No, Step_Name,
                               Status, Completed_Date, Completed_By_User, User_Role, Remarks
                        FROM dbo.Client_Activation_Step_History
                        WHERE Activation_Id = @ActivationId
                        ORDER BY Step_No ASC, Step_History_Id ASC;";

                    using var hCmd = new SqlCommand(histSql, conn);
                    hCmd.Parameters.AddWithValue("@ActivationId", actItem.ActivationId.Value);
                    using var hr = await hCmd.ExecuteReaderAsync(cancellationToken);
                    while (await hr.ReadAsync(cancellationToken))
                    {
                        actItem.StepHistory.Add(new ClientActivationStepHistoryDto
                        {
                            StepHistoryId = Convert.ToInt32(hr["Step_History_Id"]),
                            ActivationId = Convert.ToInt32(hr["Activation_Id"]),
                            StepNo = Convert.ToInt32(hr["Step_No"]),
                            StepName = hr["Step_Name"]?.ToString() ?? string.Empty,
                            Status = hr["Status"]?.ToString()?.Trim() ?? "Completed",
                            CompletedDate = Convert.ToDateTime(hr["Completed_Date"]),
                            CompletedByUser = hr["Completed_By_User"]?.ToString() ?? string.Empty,
                            UserRole = hr["User_Role"] as string,
                            Remarks = hr["Remarks"] as string
                        });
                    }
                }
            }

            // Load Technical History (dbo.Client_Technical_History)
            try
            {
                const string thSql = @"
                    IF OBJECT_ID('dbo.Client_Technical_History', 'U') IS NOT NULL
                    BEGIN
                         SELECT h.History_Id, h.Client_Id, h.Product_Code, h.Version_Label, h.Field_Name,
                               h.Previous_Value, h.New_Value, h.Change_Reason,
                               COALESCE(NULLIF(LTRIM(RTRIM(u.FullName)), ''), u.Username, h.Created_By) AS Created_By,
                               h.Created_Date
                        FROM dbo.Client_Technical_History h
                        LEFT JOIN dbo.UserMaster u ON TRY_CAST(h.Created_By AS INT) = u.UserId
                        WHERE h.Client_Id = @ClientId
                        ORDER BY h.History_Id DESC;
                    END";

                using var thCmd = new SqlCommand(thSql, conn);
                thCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var thr = await thCmd.ExecuteReaderAsync(cancellationToken);
                while (await thr.ReadAsync(cancellationToken))
                {
                    result.TechnicalHistories.Add(new ClientTechnicalHistoryDto
                    {
                        HistoryId = Convert.ToInt32(thr["History_Id"]),
                        ClientId = Convert.ToInt32(thr["Client_Id"]),
                        ProductCode = thr["Product_Code"]?.ToString()?.Trim() ?? "ERP",
                        VersionLabel = thr["Version_Label"]?.ToString()?.Trim() ?? "v1",
                        FieldName = thr["Field_Name"]?.ToString() ?? string.Empty,
                        PreviousValue = thr["Previous_Value"] as string,
                        NewValue = thr["New_Value"] as string,
                        ChangeReason = thr["Change_Reason"] as string,
                        CreatedBy = thr["Created_By"] as string,
                        CreatedDate = thr["Created_Date"] == DBNull.Value ? null : Convert.ToDateTime(thr["Created_Date"])
                    });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Could not load Client_Technical_History (table may not exist yet in database).");
            }

            // Load Documents (dbo.Client_Document)
            try
            {
                const string docSql = @"
                    IF OBJECT_ID('dbo.Client_Document', 'U') IS NOT NULL
                    BEGIN
                        SELECT d.Document_Id, d.Client_Id, d.Document_Name, d.Original_File_Name,
                               d.Stored_File_Name, d.File_Path, d.File_Extension, d.File_Size_Bytes,
                               d.Content_Type, d.Document_Category, d.Document_Version, d.Expiry_Date,
                               d.Access_Level, d.Is_Active, d.Created_By, d.Created_Date,
                               u.FullName AS CreatedByName
                        FROM dbo.Client_Document d
                        LEFT JOIN dbo.UserMaster u ON d.Created_By = u.UserId
                        WHERE d.Client_Id = @ClientId AND d.Is_Active = 'Y'
                        ORDER BY d.Document_Id DESC;
                    END";

                using var docCmd = new SqlCommand(docSql, conn);
                docCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var dr = await docCmd.ExecuteReaderAsync(cancellationToken);
                while (await dr.ReadAsync(cancellationToken))
                {
                    result.Documents.Add(new ClientDocumentDto
                    {
                        DocumentId = Convert.ToInt32(dr["Document_Id"]),
                        ClientId = Convert.ToInt32(dr["Client_Id"]),
                        DocumentName = dr["Document_Name"]?.ToString() ?? string.Empty,
                        OriginalFileName = dr["Original_File_Name"]?.ToString() ?? string.Empty,
                        StoredFileName = dr["Stored_File_Name"]?.ToString() ?? string.Empty,
                        FilePath = dr["File_Path"]?.ToString() ?? string.Empty,
                        FileExtension = dr["File_Extension"]?.ToString() ?? string.Empty,
                        FileSizeBytes = dr["File_Size_Bytes"] == DBNull.Value ? null : Convert.ToInt64(dr["File_Size_Bytes"]),
                        ContentType = dr["Content_Type"] as string,
                        DocumentCategory = dr["Document_Category"]?.ToString() ?? "Other Documents",
                        DocumentVersion = dr["Document_Version"]?.ToString() ?? "v1",
                        ExpiryDate = dr["Expiry_Date"] == DBNull.Value ? null : Convert.ToDateTime(dr["Expiry_Date"]),
                        AccessLevel = dr["Access_Level"]?.ToString() ?? "all",
                        IsActive = dr["Is_Active"]?.ToString() ?? "Y",
                        CreatedBy = Convert.ToInt32(dr["Created_By"]),
                        CreatedByName = dr["CreatedByName"] as string,
                        CreatedDate = Convert.ToDateTime(dr["Created_Date"])
                    });
                }
            }

            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Could not load Client_Document (table may not exist yet in database).");
            }

            // Load Audit Logs (dbo.Client_Audit_Log)
            try
            {
                const string auditSql = @"
                    IF OBJECT_ID('dbo.Client_Audit_Log', 'U') IS NOT NULL
                    BEGIN
                        SELECT a.Audit_Id, a.Client_Id, a.Section_Name, a.Table_Name, a.Record_Id,
                               a.Field_Name, a.Previous_Value, a.Updated_Value, a.Change_Reason,
                               a.Changed_By, a.Changed_Date,
                               ISNULL(NULLIF(LTRIM(RTRIM(u.FullName)), ''), u.Username) AS ChangedByName,
                               u.Role AS ChangedByRole
                        FROM dbo.Client_Audit_Log a
                        LEFT JOIN dbo.UserMaster u ON a.Changed_By = u.UserId
                        WHERE a.Client_Id = @ClientId
                        ORDER BY a.Audit_Id DESC;
                    END";

                using var aCmd = new SqlCommand(auditSql, conn);
                aCmd.Parameters.AddWithValue("@ClientId", clientId);
                using var ar = await aCmd.ExecuteReaderAsync(cancellationToken);
                while (await ar.ReadAsync(cancellationToken))
                {
                    result.AuditLogs.Add(new ClientAuditLogDto
                    {
                        AuditId = Convert.ToInt64(ar["Audit_Id"]),
                        ClientId = Convert.ToInt32(ar["Client_Id"]),
                        SectionName = ar["Section_Name"]?.ToString() ?? "Customer Profile",
                        TableName = ar["Table_Name"]?.ToString() ?? "Client_Master",
                        RecordId = ar["Record_Id"] == DBNull.Value ? null : Convert.ToInt32(ar["Record_Id"]),
                        FieldName = ar["Field_Name"]?.ToString() ?? string.Empty,
                        PreviousValue = ar["Previous_Value"] as string,
                        UpdatedValue = ar["Updated_Value"] as string,
                        ChangeReason = ar["Change_Reason"] as string,
                        ChangedBy = Convert.ToInt32(ar["Changed_By"]),
                        ChangedByName = ar["ChangedByName"] as string,
                        ChangedByRole = ar["ChangedByRole"] as string,
                        ChangedDate = Convert.ToDateTime(ar["Changed_Date"])
                    });
                }
            }
            catch (SqlException ex)
            {
                _logger.LogWarning(ex, "Could not load Client_Audit_Log (table may not exist yet in database).");
            }


            return result;
        }

        public async Task<IEnumerable<ClientRegistrationResponseDto>> GetAllClientRegistrationsAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<ClientRegistrationResponseDto>();
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT Client_Id, Client_Name, Client_Address, Is_Active,
                       Customer_Code, Display_Name, Customer_Type, Website, Year_Of_Establishment,
                       Industry_Category, Business_Segment, Company_Size, Number_Of_Branches,
                       Customer_Source, Relationship_Manager_Id, Customer_Status, Registration_Date, Internal_Remarks,
                       PAN_No, GST_Registration_Type, GSTIN_Primary, CIN_No, LLPIN_No, TAN_No, MSME_Reg_No, IEC_No,
                       Billing_Address, Country, State, City, District, PIN_Code, Registration_Stage
                FROM dbo.Client_Master
                ORDER BY Client_Id DESC;";

            using var cmd = new SqlCommand(sql, conn);
            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                list.Add(MapMasterRow(r));
            }

            return list;
        }

        public async Task<IEnumerable<ClientSearchRowDto>> SearchClientsAsync(ClientSearchFilterDto? filter = null, CancellationToken cancellationToken = default)
        {
            var list = new List<ClientSearchRowDto>();
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            var sql = @"
                SELECT 
                    m.Client_Id,
                    m.Customer_Code,
                    m.Client_Name,
                    m.Display_Name,
                    m.Customer_Type,
                    m.Industry_Category,
                    m.Business_Segment,
                    m.City,
                    m.State,
                    m.Country,
                    m.GSTIN_Primary,
                    m.PAN_No,
                    m.Customer_Status,
                    m.Registration_Stage,
                    m.Is_Active,
                    m.Registration_Date,
                    m.Created_Date,
                    m.Modified_Date,
                    ISNULL(br.BranchCount, 0) AS BranchCount,
                    c.Contact_Name AS PrimaryContactName,
                    c.Mobile_No AS PrimaryContactMobile,
                    c.Email AS PrimaryContactEmail,
                    c.Designation AS PrimaryContactDesignation
                FROM dbo.Client_Master m
                LEFT JOIN (
                    SELECT 
                        Client_Id, 
                        Contact_Name, 
                        Mobile_No, 
                        Email, 
                        Designation,
                        ROW_NUMBER() OVER (PARTITION BY Client_Id ORDER BY CASE WHEN Is_Primary = 'Y' THEN 0 ELSE 1 END, Contact_Id ASC) AS rn
                    FROM dbo.Client_Contact
                    WHERE Is_Active = 'Y'
                ) c ON m.Client_Id = c.Client_Id AND c.rn = 1
                LEFT JOIN (
                    SELECT Client_Id, COUNT(1) AS BranchCount
                    FROM dbo.Client_Branch
                    GROUP BY Client_Id
                ) br ON m.Client_Id = br.Client_Id
                WHERE 1=1 ";

            using var cmd = new SqlCommand();
            cmd.Connection = conn;

            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.SearchQuery))
                {
                    sql += @" AND (
                        m.Client_Name LIKE @Query 
                        OR m.Customer_Code LIKE @Query 
                        OR m.GSTIN_Primary LIKE @Query 
                        OR m.PAN_No LIKE @Query
                        OR c.Mobile_No LIKE @Query 
                        OR c.Contact_Name LIKE @Query
                    )";
                    cmd.Parameters.AddWithValue("@Query", $"%{filter.SearchQuery.Trim()}%");
                }

                if (!string.IsNullOrWhiteSpace(filter.Industry))
                {
                    sql += " AND m.Industry_Category = @Industry";
                    cmd.Parameters.AddWithValue("@Industry", filter.Industry.Trim());
                }

                if (!string.IsNullOrWhiteSpace(filter.State))
                {
                    sql += " AND m.State = @State";
                    cmd.Parameters.AddWithValue("@State", filter.State.Trim());
                }

                if (!string.IsNullOrWhiteSpace(filter.Status))
                {
                    sql += " AND m.Customer_Status = @Status";
                    cmd.Parameters.AddWithValue("@Status", filter.Status.Trim());
                }

                if (!string.IsNullOrWhiteSpace(filter.Stage))
                {
                    sql += " AND m.Registration_Stage = @Stage";
                    cmd.Parameters.AddWithValue("@Stage", filter.Stage.Trim());
                }
            }

            sql += " ORDER BY m.Client_Id DESC;";
            cmd.CommandText = sql;

            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                list.Add(new ClientSearchRowDto
                {
                    ClientId = Convert.ToInt32(r["Client_Id"]),
                    CustomerCode = r["Customer_Code"]?.ToString() ?? string.Empty,
                    ClientName = r["Client_Name"]?.ToString() ?? string.Empty,
                    DisplayName = r["Display_Name"] as string,
                    CustomerType = r["Customer_Type"] as string,
                    IndustryCategory = r["Industry_Category"] as string,
                    BusinessSegment = r["Business_Segment"] as string,
                    City = r["City"] as string,
                    State = r["State"] as string,
                    Country = r["Country"] as string ?? "India",
                    PrimaryContactName = r["PrimaryContactName"] as string,
                    PrimaryContactMobile = r["PrimaryContactMobile"] as string,
                    PrimaryContactEmail = r["PrimaryContactEmail"] as string,
                    PrimaryContactDesignation = r["PrimaryContactDesignation"] as string,
                    GstinPrimary = r["GSTIN_Primary"] as string,
                    PanNo = r["PAN_No"] as string,
                    CustomerStatus = r["Customer_Status"]?.ToString() ?? "Prospect",
                    RegistrationStage = r["Registration_Stage"]?.ToString() ?? "DRAFT",
                    IsActive = r["Is_Active"]?.ToString() ?? "Y",
                    RegistrationDate = r["Registration_Date"] != DBNull.Value ? Convert.ToDateTime(r["Registration_Date"]) : DateTime.Today,
                    CreatedDate = r["Created_Date"] != DBNull.Value ? Convert.ToDateTime(r["Created_Date"]) : null,
                    ModifiedDate = r["Modified_Date"] != DBNull.Value ? Convert.ToDateTime(r["Modified_Date"]) : null,
                    BranchCount = r["BranchCount"] != DBNull.Value ? Convert.ToInt32(r["BranchCount"]) : 0
                });
            }

            return list;
        }

        public async Task<IEnumerable<ClientDropdownOptionDto>> GetDropdownOptionsAsync(
            string mode,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(mode))
            {
                throw new ArgumentException("Parameter 'mode' is compulsory and cannot be null or empty.", nameof(mode));
            }

            var options = new List<ClientDropdownOptionDto>();
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            const string sql = @"
                DECLARE @sql NVARCHAR(MAX) = N'';
                DECLARE @m NVARCHAR(100) = UPPER(LTRIM(RTRIM(@mode)));

                IF @m = 'CUSTOMER_TYPE' OR @m = 'CT'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Customer_Type_Id AS Id,
                        ''CUSTOMER_TYPE'' AS Mode,
                        Customer_Type_Name AS Value,
                        Customer_Type_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Customer_Type_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Customer_Type_Name;';
                END
                ELSE IF @m = 'INDUSTRY_CATEGORY' OR @m = 'IC'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Industry_Category_Id AS Id,
                        ''INDUSTRY_CATEGORY'' AS Mode,
                        Industry_Category_Name AS Value,
                        Industry_Category_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Industry_Category_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Industry_Category_Name;';
                END
                ELSE IF @m = 'BUSINESS_SEGMENT' OR @m = 'BS'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Business_Segment_Id AS Id,
                        ''BUSINESS_SEGMENT'' AS Mode,
                        Business_Segment_Name AS Value,
                        Business_Segment_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Business_Segment_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Business_Segment_Name;';
                END
                ELSE IF @m = 'COMPANY_SIZE' OR @m = 'CS'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Company_Size_Id AS Id,
                        ''COMPANY_SIZE'' AS Mode,
                        Company_Size_Name AS Value,
                        Company_Size_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Company_Size_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Company_Size_Name;';
                END
                ELSE IF @m = 'CUSTOMER_SOURCE' OR @m = 'SRC'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Customer_Source_Id AS Id,
                        ''CUSTOMER_SOURCE'' AS Mode,
                        Customer_Source_Name AS Value,
                        Customer_Source_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Customer_Source_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Customer_Source_Name;';
                END
                ELSE IF @m = 'CUSTOMER_STATUS' OR @m = 'CST'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Customer_Status_Id AS Id,
                        ''CUSTOMER_STATUS'' AS Mode,
                        Customer_Status_Name AS Value,
                        Customer_Status_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Customer_Status_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Customer_Status_Name;';
                END
                ELSE IF @m = 'GST_REGISTRATION_TYPE' OR @m = 'GST_TYPE'
                BEGIN
                    SET @sql = '
                    SELECT 
                        GST_Reg_Type_Id AS Id,
                        ''GST_REGISTRATION_TYPE'' AS Mode,
                        GST_Reg_Type_Name AS Value,
                        GST_Reg_Type_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.GST_Registration_Type_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, GST_Reg_Type_Name;';
                END
                ELSE IF @m = 'COUNTRY' OR @m = 'CO'
                BEGIN
                    IF OBJECT_ID('dbo.Country_Master', 'U') IS NOT NULL
                    BEGIN
                        SET @sql = '
                        SELECT 
                            Country_Id AS Id,
                            ''COUNTRY'' AS Mode,
                            Country_Name AS Value,
                            Country_Name AS Label,
                            Country_Code AS ExtraCode,
                            Sort_Order AS SortOrder
                        FROM dbo.Country_Master
                        WHERE Is_Active = ''Y''
                        ORDER BY Sort_Order, Country_Name;';
                    END
                    ELSE
                    BEGIN
                        SET @sql = '
                        SELECT 1 AS Id, ''COUNTRY'' AS Mode, ''India'' AS Value, ''India'' AS Label, ''IN'' AS ExtraCode, 1 AS SortOrder
                        UNION ALL SELECT 2, ''COUNTRY'', ''United Arab Emirates'', ''United Arab Emirates'', ''AE'', 2
                        UNION ALL SELECT 3, ''COUNTRY'', ''Singapore'', ''Singapore'', ''SG'', 3
                        UNION ALL SELECT 4, ''COUNTRY'', ''Other'', ''Other'', NULL, 4
                        ORDER BY SortOrder, Label;';
                    END
                END
                ELSE IF @m = 'STATE' OR @m = 'ST'
                BEGIN
                    IF OBJECT_ID('dbo.State_Master', 'U') IS NOT NULL
                    BEGIN
                        SET @sql = '
                        SELECT 
                            State_Id AS Id,
                            ''STATE'' AS Mode,
                            State_Name AS Value,
                            State_Name AS Label,
                            GST_State_Code AS ExtraCode,
                            Sort_Order AS SortOrder
                        FROM dbo.State_Master
                        WHERE Is_Active = ''Y''
                        ORDER BY Sort_Order, State_Name;';
                    END
                    ELSE
                    BEGIN
                        SET @sql = '
                        SELECT 1 AS Id, ''STATE'' AS Mode, ''Maharashtra'' AS Value, ''Maharashtra'' AS Label, ''27'' AS ExtraCode, 1 AS SortOrder
                        UNION ALL SELECT 2, ''STATE'', ''Gujarat'', ''Gujarat'', ''24'', 2
                        UNION ALL SELECT 3, ''STATE'', ''Karnataka'', ''Karnataka'', ''29'', 3
                        UNION ALL SELECT 4, ''STATE'', ''Tamil Nadu'', ''Tamil Nadu'', ''33'', 4
                        UNION ALL SELECT 5, ''STATE'', ''Telangana'', ''Telangana'', ''36'', 5
                        UNION ALL SELECT 6, ''STATE'', ''Uttar Pradesh'', ''Uttar Pradesh'', ''09'', 6
                        UNION ALL SELECT 7, ''STATE'', ''Delhi'', ''Delhi'', ''07'', 7
                        UNION ALL SELECT 8, ''STATE'', ''Haryana'', ''Haryana'', ''06'', 8
                        UNION ALL SELECT 9, ''STATE'', ''Rajasthan'', ''Rajasthan'', ''08'', 9
                        UNION ALL SELECT 10, ''STATE'', ''Andhra Pradesh'', ''Andhra Pradesh'', ''37'', 10
                        UNION ALL SELECT 11, ''STATE'', ''West Bengal'', ''West Bengal'', ''19'', 11
                        UNION ALL SELECT 12, ''STATE'', ''Kerala'', ''Kerala'', ''32'', 12
                        UNION ALL SELECT 13, ''STATE'', ''Madhya Pradesh'', ''Madhya Pradesh'', ''23'', 13
                        UNION ALL SELECT 14, ''STATE'', ''Punjab'', ''Punjab'', ''03'', 14
                        UNION ALL SELECT 15, ''STATE'', ''Other'', ''Other'', NULL, 15
                        ORDER BY SortOrder, Label;';
                    END
                END
                ELSE IF @m = 'CONTACT_CATEGORY' OR @m = 'CC'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Contact_Category_Id AS Id,
                        ''CONTACT_CATEGORY'' AS Mode,
                        Contact_Category_Name AS Value,
                        Contact_Category_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Contact_Category_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Contact_Category_Name;';
                END
                ELSE IF @m = 'BRANCH_TYPE' OR @m = 'BT'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Branch_Type_Id AS Id,
                        ''BRANCH_TYPE'' AS Mode,
                        Branch_Type_Name AS Value,
                        Branch_Type_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Branch_Type_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Branch_Type_Name;';
                END
                ELSE IF @m = 'PAYMENT_TERMS' OR @m = 'PT'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Payment_Terms_Id AS Id,
                        ''PAYMENT_TERMS'' AS Mode,
                        Payment_Terms_Name AS Value,
                        Payment_Terms_Name AS Label,
                        CAST(Credit_Days AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Payment_Terms_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Payment_Terms_Name;';
                END
                ELSE IF @m = 'CURRENCY' OR @m = 'CUR'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Currency_Id AS Id,
                        ''CURRENCY'' AS Mode,
                        CAST(RTRIM(Currency_Code) AS NVARCHAR(100)) AS Value,
                        CAST(RTRIM(Currency_Code) AS NVARCHAR(100)) AS Label,
                        RTRIM(Currency_Code) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Currency_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Currency_Code;';
                END
                ELSE IF @m = 'INVOICE_FREQUENCY' OR @m = 'IF'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Invoice_Frequency_Id AS Id,
                        ''INVOICE_FREQUENCY'' AS Mode,
                        Invoice_Frequency_Name AS Value,
                        Invoice_Frequency_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Invoice_Frequency_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Invoice_Frequency_Name;';
                END
                ELSE IF @m = 'TAX_CATEGORY' OR @m = 'TC'
                BEGIN
                    SET @sql = '
                    SELECT 
                        Tax_Category_Id AS Id,
                        ''TAX_CATEGORY'' AS Mode,
                        Tax_Category_Name AS Value,
                        Tax_Category_Name AS Label,
                        CAST(NULL AS VARCHAR(20)) AS ExtraCode,
                        Sort_Order AS SortOrder
                    FROM dbo.Tax_Category_Master
                    WHERE Is_Active = ''Y''
                    ORDER BY Sort_Order, Tax_Category_Name;';
                END
                ELSE IF @m = 'RELATIONSHIP_MANAGER' OR @m = 'RM'
                BEGIN
                    SET @sql = '
                    SELECT 
                        UserId AS Id,
                        ''RELATIONSHIP_MANAGER'' AS Mode,
                        ISNULL(NULLIF(LTRIM(RTRIM(FullName)), ''''), Username) AS Value,
                        ISNULL(NULLIF(LTRIM(RTRIM(FullName)), ''''), Username) AS Label,
                        CAST(UserId AS VARCHAR(20)) AS ExtraCode,
                        UserId AS SortOrder
                    FROM dbo.UserMaster
                    WHERE IsActive = 1
                    ORDER BY Label;';
                END
                ELSE IF @m = 'USERS' OR @m = 'USER_MASTER'
                BEGIN
                    SET @sql = '
                    SELECT 
                        UserId AS Id,
                        ''USERS'' AS Mode,
                        Username AS Value,
                        ISNULL(NULLIF(LTRIM(RTRIM(FullName)), ''''), Username) AS Label,
                        CAST(LEFT(ISNULL(Role, ''User''), 20) AS VARCHAR(20)) AS ExtraCode,
                        UserId AS SortOrder
                    FROM dbo.UserMaster
                    WHERE IsActive = 1
                    ORDER BY UserId;';
                END
                ELSE
                BEGIN
                    RAISERROR('Invalid or unsupported @mode parameter.', 16, 1);
                    RETURN;
                END

                EXEC sp_executesql @sql;
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@mode", mode.Trim());

            using var r = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await r.ReadAsync(cancellationToken))
            {
                options.Add(new ClientDropdownOptionDto
                {
                    Id = Convert.ToInt32(r["Id"]),
                    Mode = r["Mode"]?.ToString() ?? string.Empty,
                    Value = r["Value"]?.ToString() ?? string.Empty,
                    Label = r["Label"]?.ToString() ?? string.Empty,
                    ExtraCode = r["ExtraCode"] == DBNull.Value ? null : r["ExtraCode"]?.ToString()?.Trim(),
                    SortOrder = Convert.ToInt32(r["SortOrder"])
                });
            }

            return options;
        }

        private static ClientRegistrationResponseDto MapMasterRow(SqlDataReader r)
        {
            int id = Convert.ToInt32(r["Client_Id"]);
            string code = r["Customer_Code"] as string ?? $"CUS-{id:D6}";

            return new ClientRegistrationResponseDto
            {
                ClientId = id,
                CustomerCode = code.Trim(),
                ClientName = r["Client_Name"]?.ToString() ?? string.Empty,
                ClientAddress = r["Client_Address"] as string,
                IsActive = r["Is_Active"]?.ToString()?.Trim() ?? "Y",
                DisplayName = r["Display_Name"] as string,
                CustomerType = r["Customer_Type"] as string,
                Website = r["Website"] as string,
                YearOfEstablishment = r["Year_Of_Establishment"] == DBNull.Value ? null : Convert.ToInt16(r["Year_Of_Establishment"]),
                IndustryCategory = r["Industry_Category"] as string,
                BusinessSegment = r["Business_Segment"] as string,
                CompanySize = r["Company_Size"] as string,
                NumberOfBranches = r["Number_Of_Branches"] == DBNull.Value ? null : Convert.ToInt32(r["Number_Of_Branches"]),
                CustomerSource = r["Customer_Source"] as string,
                RelationshipManagerId = r["Relationship_Manager_Id"] == DBNull.Value ? null : Convert.ToInt32(r["Relationship_Manager_Id"]),
                CustomerStatus = r["Customer_Status"] as string ?? "Prospect",
                RegistrationDate = r["Registration_Date"] == DBNull.Value ? DateTime.Today : Convert.ToDateTime(r["Registration_Date"]),
                InternalRemarks = r["Internal_Remarks"] as string,
                PanNo = r["PAN_No"]?.ToString()?.Trim(),
                GstRegistrationType = r["GST_Registration_Type"] as string,
                GstinPrimary = r["GSTIN_Primary"]?.ToString()?.Trim(),
                CinNo = r["CIN_No"]?.ToString()?.Trim(),
                LlpinNo = r["LLPIN_No"]?.ToString()?.Trim(),
                TanNo = r["TAN_No"]?.ToString()?.Trim(),
                MsmeRegNo = r["MSME_Reg_No"]?.ToString()?.Trim(),
                IecNo = r["IEC_No"]?.ToString()?.Trim(),
                BillingAddress = r["Billing_Address"] as string,
                Country = r["Country"] as string ?? "India",
                State = r["State"] as string,
                City = r["City"] as string,
                District = r["District"] as string,
                PinCode = r["PIN_Code"]?.ToString()?.Trim(),
                RegistrationStage = r["Registration_Stage"] as string ?? "DRAFT"
            };
        }

        public async Task<bool> AdvanceActivationStepAsync(
            AdvanceActivationStepDto dto,
            CancellationToken cancellationToken = default)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var tx = (SqlTransaction)await conn.BeginTransactionAsync(cancellationToken);

            try
            {
                string prodCode = dto.ProductCode.Trim().ToUpperInvariant();

                // 1. Ensure Activation record exists
                const string getActSql = @"
                    SELECT Activation_Id, Current_Step_No, Is_On_Hold
                    FROM dbo.Client_Product_Activation
                    WHERE Client_Id = @ClientId AND Product_Code = @ProductCode;";

                int actId = 0;
                int curStep = 1;
                string onHold = "N";

                using (var cmd = new SqlCommand(getActSql, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                    cmd.Parameters.AddWithValue("@ProductCode", prodCode);
                    using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                    if (await rdr.ReadAsync(cancellationToken))
                    {
                        actId = Convert.ToInt32(rdr["Activation_Id"]);
                        curStep = Convert.ToInt32(rdr["Current_Step_No"]);
                        onHold = rdr["Is_On_Hold"]?.ToString()?.Trim() ?? "N";
                    }
                }

                if (actId == 0)
                {
                    const string insActSql = @"
                        INSERT INTO dbo.Client_Product_Activation (
                            Client_Id, Product_Code, Current_Step_No, Activation_Status,
                            Is_On_Hold, Created_By, Created_Date
                        )
                        OUTPUT INSERTED.Activation_Id
                        VALUES (
                            @ClientId, @ProductCode, 1, 'In Progress',
                            'N', 1, GETDATE()
                        );";

                    using var insCmd = new SqlCommand(insActSql, conn, tx);
                    insCmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                    insCmd.Parameters.AddWithValue("@ProductCode", prodCode);
                    actId = Convert.ToInt32(await insCmd.ExecuteScalarAsync(cancellationToken));
                }

                if (dto.IsHoldToggle)
                {
                    string newHold = dto.NewHoldState ? "Y" : "N";
                    string newStatus = dto.NewHoldState ? "On Hold" : (curStep >= 11 ? "Completed" : "In Progress");

                    const string updateHoldSql = @"
                        UPDATE dbo.Client_Product_Activation
                        SET Is_On_Hold = @NewHold,
                            Activation_Status = @NewStatus,
                            Hold_Reason = @HoldReason,
                            Modified_Date = GETDATE()
                        WHERE Activation_Id = @ActivationId;";

                    using var hCmd = new SqlCommand(updateHoldSql, conn, tx);
                    hCmd.Parameters.AddWithValue("@NewHold", newHold);
                    hCmd.Parameters.AddWithValue("@NewStatus", newStatus);
                    hCmd.Parameters.AddWithValue("@HoldReason", DbStr(dto.Remarks));
                    hCmd.Parameters.AddWithValue("@ActivationId", actId);
                    await hCmd.ExecuteNonQueryAsync(cancellationToken);
                }
                else
                {
                    int nextStep = dto.TargetStepNo > 0 ? dto.TargetStepNo : (curStep + 1);
                    if (nextStep > 11) nextStep = 11;
                    string actStatus = nextStep >= 11 ? "Completed" : "In Progress";
                    DateTime? techDone = nextStep >= 9 ? DateTime.Now : null;
                    DateTime? goLive = nextStep >= 10 ? DateTime.Now : null;

                    const string updateStepSql = @"
                        UPDATE dbo.Client_Product_Activation
                        SET Current_Step_No = @NextStep,
                            Activation_Status = @ActStatus,
                            Tech_Done_Date = COALESCE(Tech_Done_Date, @TechDone),
                            Go_Live_Date = COALESCE(Go_Live_Date, @GoLive),
                            Modified_Date = GETDATE()
                        WHERE Activation_Id = @ActivationId;";

                    using var sCmd = new SqlCommand(updateStepSql, conn, tx);
                    sCmd.Parameters.AddWithValue("@NextStep", nextStep);
                    sCmd.Parameters.AddWithValue("@ActStatus", actStatus);
                    sCmd.Parameters.AddWithValue("@TechDone", DbVal(techDone));
                    sCmd.Parameters.AddWithValue("@GoLive", DbVal(goLive));
                    sCmd.Parameters.AddWithValue("@ActivationId", actId);
                    await sCmd.ExecuteNonQueryAsync(cancellationToken);

                    // Add history record for the completed step
                    const string addHistSql = @"
                        INSERT INTO dbo.Client_Activation_Step_History (
                            Activation_Id, Step_No, Step_Name, Status,
                            Completed_Date, Completed_By_User, User_Role, Remarks
                        )
                        VALUES (
                            @ActivationId, @StepNo, @StepName, 'Completed',
                            GETDATE(), @ByUser, @UserRole, @Remarks
                        );";

                    using var histCmd = new SqlCommand(addHistSql, conn, tx);
                    histCmd.Parameters.AddWithValue("@ActivationId", actId);
                    histCmd.Parameters.AddWithValue("@StepNo", nextStep);
                    histCmd.Parameters.AddWithValue("@StepName", string.IsNullOrWhiteSpace(dto.StepName) ? $"Step {nextStep}" : dto.StepName.Trim());
                    histCmd.Parameters.AddWithValue("@ByUser", string.IsNullOrWhiteSpace(dto.PerformedByUser) ? "User" : dto.PerformedByUser.Trim());
                    histCmd.Parameters.AddWithValue("@UserRole", DbStr(dto.UserRole));
                    histCmd.Parameters.AddWithValue("@Remarks", DbStr(dto.Remarks));
                    await histCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error advancing activation step for ClientId={ClientId}, Product={ProductCode}", dto.ClientId, dto.ProductCode);
                throw;
            }
        }

        public async Task<string> GenerateUniqueCustomerCodeAsync(string clientName, string? customerType = null, CancellationToken cancellationToken = default)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            return await GenerateUniqueCustomerCodeInternalAsync(conn, null, clientName, customerType, cancellationToken);
        }

        private static async Task<string> GenerateUniqueCustomerCodeInternalAsync(
            SqlConnection conn,
            SqlTransaction? transaction,
            string clientName,
            string? customerType,
            CancellationToken cancellationToken)
        {
            // 1. Positions 1-3: 3 letters from client name
            string cleanName = new string((clientName ?? string.Empty).Where(char.IsLetter).ToArray()).ToUpperInvariant();
            string prefix;
            if (cleanName.Length >= 3)
            {
                prefix = cleanName.Substring(0, 3);
            }
            else if (cleanName.Length > 0)
            {
                prefix = cleanName.PadRight(3, 'X');
            }
            else
            {
                prefix = "PRW";
            }

            // 2. Position 4: Entity / Customer Type letter
            char entityChar = MapCustomerTypeToEntityChar(customerType);

            // 3. Loop up to 100 attempts to guarantee uniqueness against database
            for (int attempt = 0; attempt < 100; attempt++)
            {
                // Positions 5-8: 4 Cryptographically secure random digits
                int randomDigits = RandomNumberGenerator.GetInt32(0, 10000);
                string digits4 = randomDigits.ToString("D4");

                // Position 9: 1 Cryptographically secure random uppercase letter
                char randomLetter = (char)('A' + RandomNumberGenerator.GetInt32(0, 26));

                // First 9 characters
                string nineChars = $"{prefix}{entityChar}{digits4}{randomLetter}";

                // Position 10: Mathematical Checksum Letter (Weighted Modulo 26)
                char checksumChar = CalculateChecksumChar(nineChars);

                string candidateCode = $"{nineChars}{checksumChar}";

                // Verify candidateCode does not already exist in dbo.Client_Master
                const string checkSql = "SELECT TOP 1 1 FROM dbo.Client_Master WHERE Customer_Code = @Code;";
                using var cmd = transaction != null
                    ? new SqlCommand(checkSql, conn, transaction)
                    : new SqlCommand(checkSql, conn);
                cmd.Parameters.AddWithValue("@Code", candidateCode);

                var exists = await cmd.ExecuteScalarAsync(cancellationToken);
                if (exists == null)
                {
                    return candidateCode;
                }
            }

            throw new InvalidOperationException("Failed to generate a unique customer code after multiple attempts.");
        }

        private static char MapCustomerTypeToEntityChar(string? customerType)
        {
            if (string.IsNullOrWhiteSpace(customerType)) return 'P';
            string t = customerType.Trim().ToLowerInvariant();

            if (t.Contains("private") || t.Contains("pvt")) return 'P';
            if (t.Contains("public") || t.Contains("corporate") || t.Contains("ltd")) return 'C';
            if (t.Contains("llp") || t.Contains("limited liability")) return 'L';
            if (t.Contains("partnership") || t.Contains("firm")) return 'F';
            if (t.Contains("proprietor") || t.Contains("sole")) return 'S';
            if (t.Contains("individual") || t.Contains("person")) return 'I';
            if (t.Contains("government") || t.Contains("govt") || t.Contains("psu")) return 'G';

            return 'O'; // Other
        }

        private static char CalculateChecksumChar(string nineChars)
        {
            int sum = 0;
            for (int i = 0; i < nineChars.Length; i++)
            {
                char ch = nineChars[i];
                int val = char.IsDigit(ch) ? (ch - '0') : (char.ToUpperInvariant(ch) - 'A' + 1);
                sum += val * (i + 1);
            }
            int remainder = Math.Abs(sum) % 26;
            return (char)('A' + remainder);
        }
        public async Task<bool> UpdateTechnicalConfigAsync(UpdateTechnicalConfigDto dto, CancellationToken cancellationToken = default)
        {
            if (dto == null || dto.ClientId <= 0) return false;
            if (dto.CreatedBy <= 0)
            {
                throw new ArgumentException("CreatedBy user ID is required and must be greater than 0.");
            }
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var transaction = conn.BeginTransaction();
            try
            {
                string prodCode = string.IsNullOrWhiteSpace(dto.ProductCode) ? "ERP" : dto.ProductCode.Trim().ToUpperInvariant();
                int actorUserId = dto.CreatedBy;
                string actor = actorUserId.ToString();

                // 1. Calculate next version label
                const string countSql = @"
                    SELECT COUNT(1) FROM dbo.Client_Technical_History 
                    WHERE Client_Id = @ClientId AND Product_Code = @ProductCode;";
                int histCount = 0;
                using (var cCmd = new SqlCommand(countSql, conn, transaction))
                {
                    cCmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                    cCmd.Parameters.AddWithValue("@ProductCode", prodCode);
                    var obj = await cCmd.ExecuteScalarAsync(cancellationToken);
                    histCount = (obj != null && obj != DBNull.Value) ? Convert.ToInt32(obj) : 0;
                }
                string verLabel = "v" + (histCount + 1);

                // 2. Insert into dbo.Client_Technical_History
                const string insHistSql = @"
                    INSERT INTO dbo.Client_Technical_History (
                        Client_Id, Product_Code, Version_Label, Field_Name,
                        Previous_Value, New_Value, Change_Reason, Created_By, Created_Date
                    )
                    VALUES (
                        @ClientId, @ProductCode, @VersionLabel, @FieldName,
                        @PreviousValue, @NewValue, @ChangeReason, @CreatedBy, GETDATE()
                    );";
                using (var hCmd = new SqlCommand(insHistSql, conn, transaction))
                {
                    hCmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                    hCmd.Parameters.AddWithValue("@ProductCode", prodCode);
                    hCmd.Parameters.AddWithValue("@VersionLabel", verLabel);
                    hCmd.Parameters.AddWithValue("@FieldName", string.IsNullOrWhiteSpace(dto.FieldName) ? "Configuration Update" : dto.FieldName.Trim());
                    hCmd.Parameters.AddWithValue("@PreviousValue", DbStr(dto.PreviousValue));
                    hCmd.Parameters.AddWithValue("@NewValue", DbStr(dto.NewValue));
                    hCmd.Parameters.AddWithValue("@ChangeReason", DbStr(dto.ChangeReason));
                    hCmd.Parameters.AddWithValue("@CreatedBy", actor);
                    await hCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // 3. If UpdatedConfig is provided, upsert into dbo.Client_Technical_Config
                if (dto.UpdatedConfig != null)
                {
                    const string upsertTechSql = @"
                        IF EXISTS (SELECT 1 FROM dbo.Client_Technical_Config WHERE Client_Id = @ClientId AND Product_Code = @ProductCode)
                        BEGIN
                            UPDATE dbo.Client_Technical_Config
                            SET Deployment_Type   = @DeploymentType,
                                SQL_Version       = @SqlVersion,
                                Server_Host       = @ServerHost,
                                Port              = @Port,
                                Database_Name     = @DatabaseName,
                                DB_User           = @DbUser,
                                DB_Password       = @DbPassword,
                                Connection_String = @ConnectionString,
                                Published_App_Url = @PublishedAppUrl,
                                Api_Url           = @ApiUrl,
                                Is_Active         = @IsActive,
                                Technical_Remarks = @TechnicalRemarks,
                                Modified_By       = @CreatedBy,
                                Modified_Date     = GETDATE()
                            WHERE Client_Id = @ClientId AND Product_Code = @ProductCode;
                        END
                        ELSE
                        BEGIN
                            INSERT INTO dbo.Client_Technical_Config (
                                Client_Id, Product_Code, Deployment_Type, SQL_Version,
                                Server_Host, Port, Database_Name, DB_User, DB_Password,
                                Connection_String, Published_App_Url, Api_Url,
                                Is_Active, Technical_Remarks, Created_By, Created_Date
                            )
                            VALUES (
                                @ClientId, @ProductCode, @DeploymentType, @SqlVersion,
                                @ServerHost, @Port, @DatabaseName, @DbUser, @DbPassword,
                                @ConnectionString, @PublishedAppUrl, @ApiUrl,
                                @IsActive, @TechnicalRemarks, @CreatedBy, GETDATE()
                            );
                        END";

                    var tc = dto.UpdatedConfig;
                    string deploy = tc.DeploymentType == "Standalone" ? "Standalone" : "Cloud";
                    string dbName = string.IsNullOrWhiteSpace(tc.DatabaseName) ? $"DB_{dto.ClientId}_{prodCode}" : tc.DatabaseName.Trim();

                    using var tCmd = new SqlCommand(upsertTechSql, conn, transaction);
                    tCmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                    tCmd.Parameters.AddWithValue("@ProductCode", prodCode);
                    tCmd.Parameters.AddWithValue("@DeploymentType", deploy);
                    tCmd.Parameters.AddWithValue("@SqlVersion", DbStr(tc.SqlVersion) ?? (object)"SQL Server 2022");
                    tCmd.Parameters.AddWithValue("@ServerHost", DbStr(tc.ServerHost));
                    tCmd.Parameters.AddWithValue("@Port", DbVal(tc.Port ?? 1433));
                    tCmd.Parameters.AddWithValue("@DatabaseName", dbName);
                    tCmd.Parameters.AddWithValue("@DbUser", DbStr(tc.DbUser));
                    tCmd.Parameters.AddWithValue("@DbPassword", DbStr(tc.DbPassword));
                    tCmd.Parameters.AddWithValue("@ConnectionString", DbStr(tc.ConnectionString));
                    tCmd.Parameters.AddWithValue("@PublishedAppUrl", DbStr(tc.PublishedAppUrl));
                    tCmd.Parameters.AddWithValue("@ApiUrl", DbStr(tc.ApiUrl));
                    tCmd.Parameters.AddWithValue("@IsActive", NormalizeYN(tc.IsActive, "Y"));
                    tCmd.Parameters.AddWithValue("@TechnicalRemarks", DbStr(tc.TechnicalRemarks));
                    tCmd.Parameters.AddWithValue("@CreatedBy", actor);
                    await tCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Error updating technical config for Client_Id={ClientId}", dto.ClientId);
                throw;
            }
        }

        public async Task<ClientDocumentDto> UploadDocumentAsync(
            UploadClientDocumentDto dto,
            int actorUserId = 0,
            CancellationToken cancellationToken = default)
        {
            if (actorUserId <= 0)
            {
                actorUserId = dto.CreatedBy;
            }
            if (actorUserId <= 0)
            {
                throw new ArgumentException("CreatedBy user ID is required and must be greater than 0.");
            }

            if (dto.File == null || dto.File.Length == 0)
            {
                throw new ArgumentException("No valid file uploaded.");
            }

            // 1. Prepare target client directory on C: drive
            var clientFolder = Path.Combine(_baseStoragePath, $"Client_{dto.ClientId}");
            if (!Directory.Exists(clientFolder))
            {
                Directory.CreateDirectory(clientFolder);
            }

            var originalFileName = Path.GetFileName(dto.File.FileName);
            var extension = Path.GetExtension(originalFileName);
            var safeBaseName = Path.GetFileNameWithoutExtension(originalFileName);
            // Unique filename with timestamp & short guid
            var storedFileName = $"{safeBaseName}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 6)}{extension}";
            var fullPhysicalPath = Path.Combine(clientFolder, storedFileName);

            // 2. Save physical file to C: drive
            using (var stream = new FileStream(fullPhysicalPath, FileMode.Create, FileAccess.Write))
            {
                await dto.File.CopyToAsync(stream, cancellationToken);
            }

            _logger.LogInformation("Physical file saved to disk at {FilePath}", fullPhysicalPath);

            // 3. Insert record into dbo.Client_Document
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            const string insertSql = @"
                INSERT INTO dbo.Client_Document (
                    Client_Id, Document_Name, Original_File_Name, Stored_File_Name,
                    File_Path, File_Extension, File_Size_Bytes, Content_Type,
                    Document_Category, Document_Version, Expiry_Date, Access_Level,
                    Is_Active, Created_By, Created_Date
                )
                OUTPUT INSERTED.Document_Id, INSERTED.Created_Date
                VALUES (
                    @ClientId, @DocumentName, @OriginalFileName, @StoredFileName,
                    @FilePath, @FileExtension, @FileSizeBytes, @ContentType,
                    @DocumentCategory, @DocumentVersion, @ExpiryDate, @AccessLevel,
                    'Y', @CreatedBy, GETDATE()
                );";

            int documentId;
            DateTime createdDate;

            using (var cmd = new SqlCommand(insertSql, conn))
            {
                cmd.Parameters.AddWithValue("@ClientId", dto.ClientId);
                cmd.Parameters.AddWithValue("@DocumentName", dto.DocumentName.Trim());
                cmd.Parameters.AddWithValue("@OriginalFileName", originalFileName);
                cmd.Parameters.AddWithValue("@StoredFileName", storedFileName);
                cmd.Parameters.AddWithValue("@FilePath", fullPhysicalPath);
                cmd.Parameters.AddWithValue("@FileExtension", extension);
                cmd.Parameters.AddWithValue("@FileSizeBytes", dto.File.Length);
                cmd.Parameters.AddWithValue("@ContentType", dto.File.ContentType ?? "application/octet-stream");
                cmd.Parameters.AddWithValue("@DocumentCategory", string.IsNullOrWhiteSpace(dto.Category) ? "Other Documents" : dto.Category.Trim());
                cmd.Parameters.AddWithValue("@DocumentVersion", string.IsNullOrWhiteSpace(dto.Version) ? "v1" : dto.Version.Trim());
                cmd.Parameters.AddWithValue("@ExpiryDate", dto.ExpiryDate.HasValue ? (object)dto.ExpiryDate.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@AccessLevel", string.IsNullOrWhiteSpace(dto.AccessLevel) ? "all" : dto.AccessLevel.Trim());
                cmd.Parameters.AddWithValue("@CreatedBy", actorUserId);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    documentId = Convert.ToInt32(reader["Document_Id"]);
                    createdDate = Convert.ToDateTime(reader["Created_Date"]);
                }
                else
                {
                    throw new InvalidOperationException("Failed to insert document record into database.");
                }
            }

            return new ClientDocumentDto
            {
                DocumentId = documentId,
                ClientId = dto.ClientId,
                DocumentName = dto.DocumentName,
                OriginalFileName = originalFileName,
                StoredFileName = storedFileName,
                FilePath = fullPhysicalPath,
                FileExtension = extension,
                FileSizeBytes = dto.File.Length,
                ContentType = dto.File.ContentType,
                DocumentCategory = dto.Category,
                DocumentVersion = dto.Version,
                ExpiryDate = dto.ExpiryDate,
                AccessLevel = dto.AccessLevel,
                IsActive = "Y",
                CreatedBy = actorUserId,
                CreatedDate = createdDate
            };
        }

        public async Task<IEnumerable<ClientDocumentDto>> GetDocumentsByClientIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            var list = new List<ClientDocumentDto>();
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            const string sql = @"
                SELECT d.Document_Id, d.Client_Id, d.Document_Name, d.Original_File_Name,
                       d.Stored_File_Name, d.File_Path, d.File_Extension, d.File_Size_Bytes,
                       d.Content_Type, d.Document_Category, d.Document_Version, d.Expiry_Date,
                       d.Access_Level, d.Is_Active, d.Created_By, d.Created_Date,
                       u.FullName AS CreatedByName
                FROM dbo.Client_Document d
                LEFT JOIN dbo.UserMaster u ON d.Created_By = u.UserId
                WHERE d.Client_Id = @ClientId AND d.Is_Active = 'Y'
                ORDER BY d.Document_Id DESC;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@ClientId", clientId);

            using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await rdr.ReadAsync(cancellationToken))
            {
                list.Add(new ClientDocumentDto
                {
                    DocumentId = Convert.ToInt32(rdr["Document_Id"]),
                    ClientId = Convert.ToInt32(rdr["Client_Id"]),
                    DocumentName = rdr["Document_Name"]?.ToString() ?? string.Empty,
                    OriginalFileName = rdr["Original_File_Name"]?.ToString() ?? string.Empty,
                    StoredFileName = rdr["Stored_File_Name"]?.ToString() ?? string.Empty,
                    FilePath = rdr["File_Path"]?.ToString() ?? string.Empty,
                    FileExtension = rdr["File_Extension"]?.ToString() ?? string.Empty,
                    FileSizeBytes = rdr["File_Size_Bytes"] == DBNull.Value ? null : Convert.ToInt64(rdr["File_Size_Bytes"]),
                    ContentType = rdr["Content_Type"] as string,
                    DocumentCategory = rdr["Document_Category"]?.ToString() ?? "Other Documents",
                    DocumentVersion = rdr["Document_Version"]?.ToString() ?? "v1",
                    ExpiryDate = rdr["Expiry_Date"] == DBNull.Value ? null : Convert.ToDateTime(rdr["Expiry_Date"]),
                    AccessLevel = rdr["Access_Level"]?.ToString() ?? "all",
                    IsActive = rdr["Is_Active"]?.ToString() ?? "Y",
                    CreatedBy = Convert.ToInt32(rdr["Created_By"]),
                    CreatedByName = rdr["CreatedByName"] as string,
                    CreatedDate = Convert.ToDateTime(rdr["Created_Date"])
                });
            }

            return list;
        }

        public async Task<(byte[] FileBytes, string ContentType, string DownloadFileName)?> GetDocumentFileAsync(
            int documentId,
            CancellationToken cancellationToken = default)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            const string sql = @"
                SELECT File_Path, Content_Type, Original_File_Name
                FROM dbo.Client_Document
                WHERE Document_Id = @DocumentId AND Is_Active = 'Y';";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DocumentId", documentId);

            string? filePath = null;
            string? contentType = null;
            string? originalName = null;

            using (var rdr = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                if (await rdr.ReadAsync(cancellationToken))
                {
                    filePath = rdr["File_Path"]?.ToString();
                    contentType = rdr["Content_Type"]?.ToString();
                    originalName = rdr["Original_File_Name"]?.ToString();
                }
            }

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger.LogWarning("Physical file not found for Document_Id={DocumentId} at path {FilePath}", documentId, filePath);
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            return (bytes, contentType ?? "application/octet-stream", originalName ?? Path.GetFileName(filePath));
        }

        public async Task<bool> DeleteDocumentAsync(int documentId, int actorUserId = 1, CancellationToken cancellationToken = default)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            const string sql = @"
                UPDATE dbo.Client_Document
                SET Is_Active = 'N',
                    Modified_By = @ModifiedBy,
                    Modified_Date = GETDATE()
                WHERE Document_Id = @DocumentId;";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@DocumentId", documentId);
            cmd.Parameters.AddWithValue("@ModifiedBy", actorUserId);

            int affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }
    }
}

