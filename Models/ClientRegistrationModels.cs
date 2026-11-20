using System.ComponentModel.DataAnnotations;

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

}

