using System.ComponentModel.DataAnnotations;

namespace PROWESS_CRM.Models
{
    // Domain Model
    public class UserMobileDetail
    {
        public int UMobId { get; set; }
        public int ClientId { get; set; }
        public int EmpId { get; set; }
        public string ClientMobile { get; set; } = string.Empty;
        public char IsActive { get; set; } = 'Y';
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    // Create DTO
    public class CreateUserMobileDetailDto
    {
        [Required(ErrorMessage = "Client ID is required.")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "Employee ID is required.")]
        public int EmpId { get; set; }

        [Required(ErrorMessage = "Client Mobile is required.")]
        [StringLength(15, ErrorMessage = "Client Mobile cannot exceed 15 characters.")]
        [Phone(ErrorMessage = "Invalid phone number format.")]
        public string ClientMobile { get; set; } = string.Empty;

        [RegularExpression("^[YNyn]$", ErrorMessage = "IsActive value must be 'Y' or 'N'.")]
        public char IsActive { get; set; } = 'Y';

        [Required(ErrorMessage = "CreatedBy user ID is required.")]
        public int CreatedBy { get; set; }
    }

    // Update Status DTO (Active / Deactive status update)
    public class UpdateUserMobileDetailDto
    {
        public int? UMobId { get; set; }

        [Required(ErrorMessage = "Client ID is required.")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "Employee ID is required.")]
        public int EmpId { get; set; }

        [RegularExpression("^[YNyn]$", ErrorMessage = "IsActive value must be 'Y' or 'N'.")]
        public char IsActive { get; set; } = 'Y';

        [Required(ErrorMessage = "ModifiedBy user ID is required.")]
        public int ModifiedBy { get; set; }
    }

    // Response DTO
    public class UserMobileDetailResponseDto
    {
        public int UMobId { get; set; }
        public int ClientId { get; set; }
        public int EmpId { get; set; }
        public string ClientMobile { get; set; } = string.Empty;
        public char IsActive { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    // User Validation Request DTO
    public class UserValidationRequestDto
    {
        [Required(ErrorMessage = "Mobile number is required.")]
        [StringLength(15, ErrorMessage = "Mobile number cannot exceed 15 characters.")]
        public string MobileNo { get; set; } = string.Empty;
    }

    // User Validation Response DTO
    public class UserValidationResponseDto
    {
        public string Status { get; set; } = "N"; // 'Y' if user mobile exists & active, and client is active
        public string Message { get; set; } = string.Empty;
        public bool IsExist { get; set; }
        public int? ClientId { get; set; }
        public string? ClientName { get; set; }
        public int? EmpId { get; set; }
        public string? HrmsApiLink { get; set; }
    }
}

