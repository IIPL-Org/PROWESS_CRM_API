using System.ComponentModel.DataAnnotations;

namespace PROWESS_CRM.Models
{
    // Domain Model
    public class ClientMaster
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string? ClientAddress { get; set; }
        public string? HrmsAdminLink { get; set; }
        public string? HrmsApiLink { get; set; }
        public string? HrmsEssLink { get; set; }
        public char HrmsAdminAccess { get; set; } = 'N';
        public char HrmsApiAccess { get; set; } = 'N';
        public char HrmsEssAccess { get; set; } = 'N';
        public char HrmsAppAccess { get; set; } = 'N';
        public char IsActive { get; set; } = 'Y';
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public List<int> ModuleIds { get; set; } = new List<int>();
        public List<int> EsModuleIds { get; set; } = new List<int>();
        public List<int> ProwessModuleIds { get; set; } = new List<int>();

    }

    // Create DTO
    public class CreateClientDto
    {
        [Required(ErrorMessage = "Client name is required.")]
        [StringLength(200, ErrorMessage = "Client name cannot exceed 200 characters.")]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ClientAddress { get; set; }

        [StringLength(500)]
        public string? HrmsAdminLink { get; set; }

        [StringLength(500)]
        public string? HrmsApiLink { get; set; }

        [StringLength(500)]
        public string? HrmsEssLink { get; set; }

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsAdminAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsApiAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsEssAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsAppAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "IsActive value must be 'Y' or 'N'.")]
        public char IsActive { get; set; } = 'Y';

        [Required(ErrorMessage = "CreatedBy user ID is required.")]
        public int CreatedBy { get; set; }
    }

    // Update DTO
    public class UpdateClientDto
    {
        [Required(ErrorMessage = "Client ID is required.")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "Client name is required.")]
        [StringLength(200, ErrorMessage = "Client name cannot exceed 200 characters.")]
        public string ClientName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ClientAddress { get; set; }

        [StringLength(500)]
        public string? HrmsAdminLink { get; set; }

        [StringLength(500)]
        public string? HrmsApiLink { get; set; }

        [StringLength(500)]
        public string? HrmsEssLink { get; set; }

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsAdminAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsApiAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsEssAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "Access value must be 'Y' or 'N'.")]
        public char HrmsAppAccess { get; set; } = 'N';

        [RegularExpression("^[YNyn]$", ErrorMessage = "IsActive value must be 'Y' or 'N'.")]
        public char IsActive { get; set; } = 'Y';

        [Required(ErrorMessage = "ModifiedBy user ID is required.")]
        public int ModifiedBy { get; set; }
    }

    public class AssignModulesDto
    {
        [Required(ErrorMessage = "Client ID is required.")]
        public int ClientId { get; set; }

        public List<int> ModuleIds { get; set; } = new List<int>();
    }

    // Response DTO
    public class ClientResponseDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string? ClientAddress { get; set; }
        public string? HrmsAdminLink { get; set; }
        public string? HrmsApiLink { get; set; }
        public string? HrmsEssLink { get; set; }
        public char HrmsAdminAccess { get; set; }
        public char HrmsApiAccess { get; set; }
        public char HrmsEssAccess { get; set; }
        public char HrmsAppAccess { get; set; }
        public char IsActive { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public List<int> ModuleIds { get; set; } = new List<int>();
        public List<int> EsModuleIds { get; set; } = new List<int>();
        public List<int> ProwessModuleIds { get; set; } = new List<int>();

    }

    // Generic API Response Wrapper
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponse<T> SuccessResponse(T data, string message = "Operation completed successfully.")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                Errors = null
            };
        }

        public static ApiResponse<T> FailureResponse(string message, List<string>? errors = null)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = default,
                Errors = errors ?? new List<string>()
            };
        }
    }
}
