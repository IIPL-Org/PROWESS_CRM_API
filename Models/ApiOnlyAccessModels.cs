using System.ComponentModel.DataAnnotations;

namespace PROWESS_CRM.Models
{
    public class ApiGroupDto
    {
        public int SubmenuId { get; set; }
        public string SubmenuName { get; set; } = string.Empty;
        public int ParentId { get; set; }
        public int MenuId { get; set; }
        public int ModuleId { get; set; }
        public string? ModulePrefix { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public char IsActive { get; set; } = 'Y';
    }

    public class ApiMethodDto
    {
        public int MethodId { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public char IsActive { get; set; } = 'Y';
    }

    public class ProwessApiMasterDto
    {
        public int ApiMasterId { get; set; }
        public int ModuleId { get; set; }
        public string? ModuleCode { get; set; }
        public int SubmenuId { get; set; }
        public int MethodId { get; set; }
        public string MethodName { get; set; } = string.Empty;
        public string EndpointPath { get; set; } = string.Empty;
        public string ApiName { get; set; } = string.Empty;
        public string ApiCode { get; set; } = string.Empty;
        public string Status { get; set; } = "Active";
        public DateTime? DeprecatedDate { get; set; }
        public string RightsAction { get; set; } = "View";
        public string? Summary { get; set; }
        public string? RequestSample { get; set; }
        public string? ResponseSample { get; set; }
        public char IsActive { get; set; } = 'Y';
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public int? ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
    }

    public class SaveProwessApiRequest
    {
        public int? ApiMasterId { get; set; }

        [Required]
        public int ModuleId { get; set; }

        [Required]
        public int SubmenuId { get; set; }

        public int MethodId { get; set; }

        [Required]
        public string MethodName { get; set; } = string.Empty;

        [Required]
        public string EndpointPath { get; set; } = string.Empty;

        [Required]
        public string ApiName { get; set; } = string.Empty;

        [Required]
        public string ApiCode { get; set; } = string.Empty;

        public string Status { get; set; } = "Active";
        public DateTime? DeprecatedDate { get; set; }

        [Required]
        public string RightsAction { get; set; } = "View";

        public string? Summary { get; set; }
        public string? RequestSample { get; set; }
        public string? ResponseSample { get; set; }
        public int? CreatedBy { get; set; }
        public int? ModifiedBy { get; set; }
    }
}
