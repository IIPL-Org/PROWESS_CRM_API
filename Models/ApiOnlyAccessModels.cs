
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;


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

    public class ProwessRollbackLogDto
    {
        public long LogId { get; set; }
        public int? ClientId { get; set; }
        public string? ClientName { get; set; }
        public string OperationType { get; set; } = "API_CUSTOMER_MAPPING_SAVE";
        public string Reason { get; set; } = string.Empty;
        public string? ErrorDetails { get; set; }
        public string? Payload { get; set; }
        public int? CreatedBy { get; set; } = 1;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }


    public class ApiErrorLogBatchRequest
    {
        [JsonPropertyName("client_id")]
        public int? ClientId { get; set; }

        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        [JsonPropertyName("source_api_url")]
        public string? SourceApiUrl { get; set; }

        [JsonPropertyName("logs")]
        public List<ApiErrorLogItemRequest> Logs { get; set; } = new();
    }

    public class ApiErrorLogItemRequest
    {
        [JsonPropertyName("local_log_id")]
        public long? LocalLogId { get; set; }

        [JsonPropertyName("client_id")]
        public int? ClientId { get; set; }

        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        [JsonPropertyName("source_api_url")]
        public string? SourceApiUrl { get; set; }

        [JsonPropertyName("user_id")]
        public int? UserId { get; set; }

        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }

        [JsonPropertyName("api_master_id")]
        public int? ApiMasterId { get; set; }

        [JsonPropertyName("api_code")]
        public string? ApiCode { get; set; }

        [JsonPropertyName("method_name")]
        public string? MethodName { get; set; }

        [JsonPropertyName("endpoint_path")]
        public string EndpointPath { get; set; } = string.Empty;

        [JsonPropertyName("environment_mode")]
        public string? EnvironmentMode { get; set; }

        [JsonPropertyName("query_params")]
        public string? QueryParams { get; set; }

        [JsonPropertyName("request_body")]
        public string? RequestBody { get; set; }

        [JsonPropertyName("request_headers")]
        public string? RequestHeaders { get; set; }

        [JsonPropertyName("http_status")]
        public int HttpStatus { get; set; }

        [JsonPropertyName("error_code")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }

        [JsonPropertyName("exception_detail")]
        public string? ExceptionDetail { get; set; }

        [JsonPropertyName("client_ip")]
        public string? ClientIp { get; set; }

        [JsonPropertyName("occurred_at")]
        public DateTime OccurredAt { get; set; } = DateTime.Now;
    }

    public class ApiErrorLogDto
    {
        public long ErrorLogId { get; set; }
        public int? ClientId { get; set; }
        public string? ClientName { get; set; }
        public string? SourceApiUrl { get; set; }
        public long? LocalLogId { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public int? ApiMasterId { get; set; }
        public string? ApiCode { get; set; }
        public string? MethodName { get; set; }
        public string EndpointPath { get; set; } = string.Empty;
        public string? EnvironmentMode { get; set; }
        public string? QueryParams { get; set; }
        public string? RequestBody { get; set; }
        public string? RequestHeaders { get; set; }
        public int HttpStatus { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ExceptionDetail { get; set; }
        public string? ClientIp { get; set; }
        public string? LogFilePath { get; set; }
        public DateTime OccurredAt { get; set; }
        public DateTime ReceivedDate { get; set; }
    }

    public class ApiErrorLogSummaryDto
    {
        public int TotalErrors { get; set; }
        public int ClientErrors4xx { get; set; }
        public int ServerErrors5xx { get; set; }
        public int TodayErrors { get; set; }
        public int AffectedCompanies { get; set; }
    }

    public class CompanyErrorCountDto
    {
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public int ErrorCount { get; set; }
        public DateTime? LastErrorAt { get; set; }
    }

    public class ApiErrorLogQueryResultDto
    {
        public List<ApiErrorLogDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public ApiErrorLogSummaryDto Summary { get; set; } = new();
        public List<CompanyErrorCountDto> CompanyCounts { get; set; } = new();
    }

    public class ApiErrorLogIngestResultDto
    {
        public int InsertedCount { get; set; }
        public List<string> LogFiles { get; set; } = new();
    }

    public class CompanyLogFileDto
    {
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string FolderName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime LastModifiedUtc { get; set; }
    }

    public class CompanyLogFileContentDto
    {
        public int? ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public int TotalLines { get; set; }
        public string Content { get; set; } = string.Empty;
    }


    public class CustomerApiUsageSyncRequest
    {
        [JsonPropertyName("client_id")]
        public int ClientId { get; set; }

        [JsonPropertyName("client_name")]
        public string? ClientName { get; set; }

        [JsonPropertyName("source_api_url")]
        public string? SourceApiUrl { get; set; }

        [JsonPropertyName("items")]
        public List<CustomerApiUsageSyncItem> Items { get; set; } = new();
    }

    public class CustomerApiUsageSyncItem
    {
        [JsonPropertyName("api_master_id")]
        public int ApiMasterId { get; set; }

        [JsonPropertyName("api_code")]
        public string? ApiCode { get; set; }

        [JsonPropertyName("method_name")]
        public string? MethodName { get; set; }

        [JsonPropertyName("endpoint_path")]
        public string? EndpointPath { get; set; }

        [JsonPropertyName("api_name")]
        public string? ApiName { get; set; }

        [JsonPropertyName("monthly_quota")]
        public long? MonthlyQuota { get; set; }

        [JsonPropertyName("usage_month")]
        public int UsageMonth { get; set; }

        [JsonPropertyName("hit_count")]
        public long HitCount { get; set; }

        [JsonPropertyName("last_hit_date")]
        public DateTime? LastHitDate { get; set; }
    }

    public class CustomerApiUsageDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public int ApiMasterId { get; set; }
        public string ApiCode { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string EndpointPath { get; set; } = string.Empty;
        public string ApiName { get; set; } = string.Empty;
        public int UsageMonth { get; set; }
        public long HitCount { get; set; }
        public long? MonthlyQuota { get; set; }
        public long? RemainingQuota { get; set; }
        public double? UsagePercent { get; set; }
        public DateTime? LastHitDate { get; set; }
        public DateTime? LastSyncedDate { get; set; }
        public string? SourceApiUrl { get; set; }
    }

    public class CustomerApiUsageSummaryDto
    {
        public long TotalHits { get; set; }
        public long TotalQuota { get; set; }
        public int ActiveApisUsed { get; set; }
        public int TotalLicensedApis { get; set; }
        public int CustomersWithUsage { get; set; }
        public int NearOrOverLimitCount { get; set; }
    }

    public class CompanyUsageSummaryDto
    {
        public int ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public long TotalHits { get; set; }
        public long TotalQuota { get; set; }
        public int ApiCount { get; set; }
        public DateTime? LastHitDate { get; set; }
    }

    public class CustomerApiUsageQueryResultDto
    {
        public int UsageMonth { get; set; }
        public List<int> AvailableMonths { get; set; } = new();
        public CustomerApiUsageSummaryDto Summary { get; set; } = new();
        public List<CompanyUsageSummaryDto> CompanySummaries { get; set; } = new();
        public List<CustomerApiUsageDto> Items { get; set; } = new();
    }



}
