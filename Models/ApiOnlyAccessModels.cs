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

    public class client_master_dto
    {
        public int client_id { get; set; }
        public string client_name { get; set; } = string.Empty;
        public string? hrms_admin_link { get; set; }
        public string? hrms_api_link { get; set; }
        public char hrms_admin_access { get; set; } = 'N';
        public char hrms_api_access { get; set; } = 'N';
        public char is_active { get; set; } = 'Y';
        public List<int> module_ids { get; set; } = new();
    }

    public class api_customer_mapping_dto
    {
        public int api_customer_mapping_id { get; set; }
        public int client_id { get; set; }
        public string? client_name { get; set; }
        public int api_master_id { get; set; }
        public string api_code { get; set; } = string.Empty;
        public string onward_exposure { get; set; } = "Internal";
        public string? consumer_types { get; set; }
        public string max_data_scope { get; set; } = "Own";
        public string sensitivity { get; set; } = "Normal";
        public string? credentials { get; set; }
        public int? max_rate_per_min { get; set; }
        public long? monthly_quota { get; set; }
        public char require_ip_whitelist { get; set; } = 'N';
        public char require_idempotency_key { get; set; } = 'N';
        public char sandbox_available { get; set; } = 'N';
        public DateTime? valid_till { get; set; }
        public char is_active { get; set; } = 'Y';
        public int created_by { get; set; } = 1;
        public DateTime created_date { get; set; } = DateTime.Now;
        public int? modified_by { get; set; }
        public DateTime? modified_date { get; set; }
    }

    public class save_customer_mappings_request
    {
        public int client_id { get; set; }
        public string? client_name { get; set; }
        public List<api_customer_mapping_dto> mappings { get; set; } = new();
    }
}
