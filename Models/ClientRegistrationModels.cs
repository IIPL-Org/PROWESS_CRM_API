using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace PROWESS_CRM.Models
{
    public class ClientRegistrationRequestDto
    {
        public int? ClientId { get; set; }

        // 1. dbo.Client_Master — Basic Information
        [Required(ErrorMessage = "Customer Legal Name (Client_Name) is required.")]
        [StringLength(200)]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(10)]
        public string? CustomerCode { get; set; }

        [StringLength(200)]
        public string? DisplayName { get; set; }

        [StringLength(50)]
        public string? CustomerType { get; set; }

        [StringLength(200)]
        public string? Website { get; set; }

        public short? YearOfEstablishment { get; set; }

        [StringLength(100)]
        public string? IndustryCategory { get; set; }

        [StringLength(100)]
        public string? BusinessSegment { get; set; }

        [StringLength(20)]
        public string? CompanySize { get; set; }

        public int? NumberOfBranches { get; set; }

        [StringLength(50)]
        public string? CustomerSource { get; set; }

        public int? RelationshipManagerId { get; set; }

        [StringLength(20)]
        public string CustomerStatus { get; set; } = "Prospect";

        public DateTime? RegistrationDate { get; set; }

        [StringLength(1000)]
        public string? InternalRemarks { get; set; }

        // 1b. dbo.Client_Master — Statutory & GST
        [StringLength(10)]
        public string? PanNo { get; set; }

        [StringLength(20)]
        public string? GstRegistrationType { get; set; }

        [StringLength(15)]
        public string? GstinPrimary { get; set; }

        [StringLength(21)]
        public string? CinNo { get; set; }

        [StringLength(8)]
        public string? LlpinNo { get; set; }

        [StringLength(10)]
        public string? TanNo { get; set; }

        [StringLength(25)]
        public string? MsmeRegNo { get; set; }

        [StringLength(10)]
        public string? IecNo { get; set; }

        [StringLength(500)]
        public string? ClientAddress { get; set; }

        [StringLength(500)]
        public string? BillingAddress { get; set; }

        [StringLength(100)]
        public string? Country { get; set; } = "India";

        [StringLength(100)]
        public string? State { get; set; }

        [StringLength(100)]
        public string? City { get; set; }

        [StringLength(100)]
        public string? District { get; set; }

        [StringLength(6)]
        public string? PinCode { get; set; }

        // Workflow & Audit fields on Client_Master
        [StringLength(20)]
        public string RegistrationStage { get; set; } = "DRAFT";

        public string IsActive { get; set; } = "Y";

        public int CreatedBy { get; set; } = 1;

        // 2. dbo.Client_Contact (1 : many)
        public List<ClientContactDto> Contacts { get; set; } = new();

        // 3. dbo.Client_Branch (1 : many)
        public List<ClientBranchDto> Branches { get; set; } = new();

        // 4. dbo.Client_Commercial (1 : 1)
        public ClientCommercialDto? Commercial { get; set; }

        // 5. dbo.Client_Technical_Config (1 : many, per product code: ADMIN, HRMS, ERP, TICKET)
        public List<ClientTechnicalDto> TechnicalConfigs { get; set; } = new();

        // 6. dbo.Client_Subscription (1 : many, per product code: ADMIN, HRMS, ERP, TICKET)
        public List<ClientSubscriptionDto> Subscriptions { get; set; } = new();

        // 7. dbo.Client_Product_Activation (1 : many, per product code: ADMIN, HRMS, ERP, TICKET)
        public List<ClientActivationDto> Activations { get; set; } = new();

        // 8. dbo.Client_Technical_History (1 : many, version history of technical configuration changes)
        public List<ClientTechnicalHistoryDto> TechnicalHistories { get; set; } = new();
        public List<ClientAuditLogDto> AuditLogs { get; set; } = new();
    }

    public class ClientContactDto
    {
        public int? ContactId { get; set; }

        [Required(ErrorMessage = "Contact person name is required.")]
        [StringLength(150)]
        public string ContactName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Designation { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [Required(ErrorMessage = "Contact category is required.")]
        [StringLength(50)]
        public string ContactCategory { get; set; } = "Primary Contact";

        [Required(ErrorMessage = "Mobile number is required.")]
        [StringLength(15)]
        public string MobileNo { get; set; } = string.Empty;

        [StringLength(15)]
        public string? AlternateMobileNo { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [StringLength(150)]
        public string Email { get; set; } = string.Empty;

        public string IsPrimary { get; set; } = "N";
        public string IsActive { get; set; } = "Y";
    }

    public class ClientBranchDto
    {
        public int? BranchId { get; set; }

        [StringLength(10)]
        public string? BranchCode { get; set; }

        [Required(ErrorMessage = "Branch name is required.")]
        [StringLength(150)]
        public string BranchName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Branch type is required.")]
        [StringLength(50)]
        public string BranchType { get; set; } = "Head Office";

        [StringLength(15)]
        public string? Gstin { get; set; }

        [Required(ErrorMessage = "Branch address is required.")]
        [StringLength(500)]
        public string Address { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Country { get; set; } = "India";

        [Required(ErrorMessage = "Branch state is required.")]
        [StringLength(100)]
        public string State { get; set; } = string.Empty;

        [Required(ErrorMessage = "Branch city is required.")]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [StringLength(6)]
        public string? PinCode { get; set; }

        [StringLength(150)]
        public string? ContactPerson { get; set; }

        [StringLength(15)]
        public string? MobileNo { get; set; }

        [StringLength(150)]
        public string? Email { get; set; }

        public string IsHeadOffice { get; set; } = "N";

        [StringLength(20)]
        public string BranchStatus { get; set; } = "Active";
    }

    public class ClientCommercialDto
    {
        [StringLength(20)]
        public string? AccountCode { get; set; }

        [StringLength(200)]
        public string? BillingEntity { get; set; }

        [StringLength(15)]
        public string? BillingGstin { get; set; }

        [StringLength(150)]
        public string? BillingContactPerson { get; set; }

        [StringLength(150)]
        public string? BillingEmail { get; set; }

        [StringLength(500)]
        public string? BillingAddress { get; set; }

        [StringLength(20)]
        public string? PaymentTerms { get; set; }

        public int? CreditPeriodDays { get; set; }

        public decimal? CreditLimit { get; set; }

        [StringLength(3)]
        public string Currency { get; set; } = "INR";

        [StringLength(30)]
        public string? InvoiceFrequency { get; set; }

        public DateTime? ContractStartDate { get; set; }

        public DateTime? ContractEndDate { get; set; }

        [StringLength(50)]
        public string? AgreementRefNo { get; set; }

        [StringLength(50)]
        public string? CustomerPoRef { get; set; }

        public string? TdsApplicable { get; set; }

        public string? GstApplicable { get; set; }

        [StringLength(60)]
        public string? TaxCategory { get; set; }

        [StringLength(1000)]
        public string? AccountsRemarks { get; set; }
    }

    public class ClientRegistrationResponseDto
    {
        public int ClientId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? CustomerType { get; set; }
        public string? Website { get; set; }
        public short? YearOfEstablishment { get; set; }
        public string? IndustryCategory { get; set; }
        public string? BusinessSegment { get; set; }
        public string? CompanySize { get; set; }
        public int? NumberOfBranches { get; set; }
        public string? CustomerSource { get; set; }
        public int? RelationshipManagerId { get; set; }
        public string CustomerStatus { get; set; } = "Prospect";
        public DateTime RegistrationDate { get; set; }
        public string? InternalRemarks { get; set; }
        public string? PanNo { get; set; }
        public string? GstRegistrationType { get; set; }
        public string? GstinPrimary { get; set; }
        public string? CinNo { get; set; }
        public string? LlpinNo { get; set; }
        public string? TanNo { get; set; }
        public string? MsmeRegNo { get; set; }
        public string? IecNo { get; set; }
        public string? ClientAddress { get; set; }
        public string? BillingAddress { get; set; }
        public string? Country { get; set; }
        public string? State { get; set; }
        public string? City { get; set; }
        public string? District { get; set; }
        public string? PinCode { get; set; }
        public string RegistrationStage { get; set; } = "DRAFT";
        public string IsActive { get; set; } = "Y";
        public List<ClientContactDto> Contacts { get; set; } = new();
        public List<ClientBranchDto> Branches { get; set; } = new();
        public ClientCommercialDto? Commercial { get; set; }
        public List<ClientTechnicalDto> TechnicalConfigs { get; set; } = new();
        public List<ClientSubscriptionDto> Subscriptions { get; set; } = new();
        public List<ClientActivationDto> Activations { get; set; } = new();
        public List<ClientTechnicalHistoryDto> TechnicalHistories { get; set; } = new();
        public List<ClientDocumentDto> Documents { get; set; } = new();
        public List<ClientAuditLogDto> AuditLogs { get; set; } = new();

    }

    public class ClientTechnicalDto
    {
        public int? ConfigId { get; set; }

        [Required(ErrorMessage = "Product Code is required.")]
        [StringLength(20)]
        public string ProductCode { get; set; } = "ERP";

        [Required(ErrorMessage = "Deployment Type is required.")]
        [StringLength(30)]
        public string DeploymentType { get; set; } = "Cloud";

        [StringLength(50)]
        public string? SqlVersion { get; set; } = "SQL Server 2022";

        [StringLength(150)]
        public string? ServerHost { get; set; }

        public int? Port { get; set; } = 1433;

        [StringLength(100)]
        public string? DatabaseName { get; set; }

        [StringLength(100)]
        public string? DbUser { get; set; }

        [StringLength(255)]
        public string? DbPassword { get; set; }

        [StringLength(1000)]
        public string? ConnectionString { get; set; }

        [StringLength(255)]
        public string? PublishedAppUrl { get; set; }

        [StringLength(255)]
        public string? ApiUrl { get; set; }

        [StringLength(1)]
        public string IsActive { get; set; } = "Y";

        [StringLength(1000)]
        public string? TechnicalRemarks { get; set; }
    }

    public class ClientSubscriptionDto
    {
        public int? SubscriptionId { get; set; }

        [Required(ErrorMessage = "Product Code is required.")]
        [StringLength(20)]
        public string ProductCode { get; set; } = "ERP";

        [Required(ErrorMessage = "Subscription Type is required.")]
        [StringLength(50)]
        public string SubscriptionType { get; set; } = "Annual";

        [Required(ErrorMessage = "Edition is required.")]
        [StringLength(50)]
        public string Edition { get; set; } = "Standard";

        [StringLength(50)]
        public string? LicenseType { get; set; } = "Named user";

        public int LicensedUsers { get; set; } = 1;
        public int ActiveUsers { get; set; } = 0;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? RenewalDate { get; set; }

        [StringLength(30)]
        public string SubscriptionStatus { get; set; } = "Pending Approval";

        public int? BranchLimit { get; set; } = 1;
        public int? DbLimit { get; set; } = 1;

        [StringLength(100)]
        public string? TechOwner { get; set; }

        [StringLength(100)]
        public string? CommercialOwner { get; set; }

        [StringLength(1000)]
        public string? SubscriptionRemarks { get; set; }

        [StringLength(1)]
        public string IsActive { get; set; } = "Y";
    }

    public class ClientActivationDto
    {
        public int? ActivationId { get; set; }

        [Required(ErrorMessage = "Product Code is required.")]
        [StringLength(20)]
        public string ProductCode { get; set; } = "ERP";

        public int CurrentStepNo { get; set; } = 1;

        [StringLength(30)]
        public string ActivationStatus { get; set; } = "In Progress";

        [StringLength(1)]
        public string IsOnHold { get; set; } = "N";

        [StringLength(500)]
        public string? HoldReason { get; set; }

        [StringLength(100)]
        public string? TechOwner { get; set; }

        public DateTime? TechDoneDate { get; set; }
        public DateTime? GoLiveDate { get; set; }

        public List<ClientActivationStepHistoryDto> StepHistory { get; set; } = new();
    }

    public class ClientActivationStepHistoryDto
    {
        public int? StepHistoryId { get; set; }
        public int ActivationId { get; set; }
        public int StepNo { get; set; }

        [StringLength(100)]
        public string StepName { get; set; } = string.Empty;

        [StringLength(30)]
        public string Status { get; set; } = "Completed";

        public DateTime CompletedDate { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string CompletedByUser { get; set; } = string.Empty;

        [StringLength(50)]
        public string? UserRole { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class AdvanceActivationStepDto
    {
        [Required]
        public int ClientId { get; set; }

        [Required]
        [StringLength(20)]
        public string ProductCode { get; set; } = "ERP";

        public int TargetStepNo { get; set; }

        [StringLength(100)]
        public string StepName { get; set; } = string.Empty;

        [StringLength(100)]
        public string PerformedByUser { get; set; } = string.Empty;

        [StringLength(50)]
        public string? UserRole { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        public bool IsHoldToggle { get; set; } = false;
        public bool NewHoldState { get; set; } = false;
    }

    public class ClientDropdownOptionDto
    {
        public int Id { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? ExtraCode { get; set; }
        public int SortOrder { get; set; }
    }

    public class ClientSearchRowDto
    {
        public int ClientId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string? CustomerType { get; set; }
        public string? IndustryCategory { get; set; }
        public string? BusinessSegment { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Country { get; set; }
        public string? PrimaryContactName { get; set; }
        public string? PrimaryContactMobile { get; set; }
        public string? PrimaryContactEmail { get; set; }
        public string? PrimaryContactDesignation { get; set; }
        public string? GstinPrimary { get; set; }
        public string? PanNo { get; set; }
        public string CustomerStatus { get; set; } = "Prospect";
        public string RegistrationStage { get; set; } = "DRAFT";
        public string IsActive { get; set; } = "Y";
        public DateTime RegistrationDate { get; set; }
        public DateTime? CreatedDate { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public int BranchCount { get; set; }
    }

    public class ClientSearchFilterDto
    {
        public string? SearchQuery { get; set; }
        public string? Industry { get; set; }
        public string? State { get; set; }
        public string? Status { get; set; }
        public string? Stage { get; set; }
    }

    public class ClientTechnicalHistoryDto
    {
        public int? HistoryId { get; set; }
        public int? ClientId { get; set; }
        public string ProductCode { get; set; } = "ERP";
        public string VersionLabel { get; set; } = "v1";
        public string FieldName { get; set; } = string.Empty;
        public string? PreviousValue { get; set; }
        public string? NewValue { get; set; }
        public string? ChangeReason { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? CreatedDate { get; set; }
    }

    public class UpdateTechnicalConfigDto
    {
        public int ClientId { get; set; }
        public string ProductCode { get; set; } = "ERP";
        public string? FieldKey { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? PreviousValue { get; set; }
        public string NewValue { get; set; } = string.Empty;
        public string? ChangeReason { get; set; }
        [Required(ErrorMessage = "CreatedBy user ID is required.")]
        public int CreatedBy { get; set; }
        public ClientTechnicalDto? UpdatedConfig { get; set; }
    }

    public class ClientDocumentDto
    {
        public int DocumentId { get; set; }
        public int ClientId { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string StoredFileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string FileExtension { get; set; } = string.Empty;
        public long? FileSizeBytes { get; set; }
        public string? ContentType { get; set; }
        public string DocumentCategory { get; set; } = "Other Documents";
        public string DocumentVersion { get; set; } = "v1";
        public DateTime? ExpiryDate { get; set; }
        public string AccessLevel { get; set; } = "all";
        public string IsActive { get; set; } = "Y";
        public int CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class UploadClientDocumentDto
    {
        [Required(ErrorMessage = "ClientId is required.")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "Document Name is required.")]
        [StringLength(200)]
        public string DocumentName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        [StringLength(100)]
        public string Category { get; set; } = "Other Documents";

        [StringLength(20)]
        public string Version { get; set; } = "v1";

        public DateTime? ExpiryDate { get; set; }

        [StringLength(50)]
        public string AccessLevel { get; set; } = "all";

        [Required(ErrorMessage = "File is required.")]
        public IFormFile File { get; set; } = null!;

        [Required(ErrorMessage = "CreatedBy user ID is required.")]
        public int CreatedBy { get; set; }

    }

    public class ClientAuditLogDto
    {
        public long? AuditId { get; set; }
        public int ClientId { get; set; }
        public string SectionName { get; set; } = "Customer Profile";
        public string TableName { get; set; } = "Client_Master";
        public int? RecordId { get; set; }
        public string FieldName { get; set; } = string.Empty;
        public string? PreviousValue { get; set; }
        public string? UpdatedValue { get; set; }
        public string? ChangeReason { get; set; }
        public int ChangedBy { get; set; } = 1;
        public string? ChangedByName { get; set; }
        public string? ChangedByRole { get; set; }
        public DateTime? ChangedDate { get; set; }
    }

}

