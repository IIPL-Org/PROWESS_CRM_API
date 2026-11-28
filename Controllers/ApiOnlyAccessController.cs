using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using Microsoft.Data.SqlClient;
using PROWESS_CRM.Models;
using PROWESS_CRM.Services;

namespace PROWESS_CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ApiOnlyAccessController : ControllerBase
    {
        private readonly IApiOnlyAccessService _apiOnlyAccessService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ApiOnlyAccessController> _logger;

        public ApiOnlyAccessController(
            IApiOnlyAccessService apiOnlyAccessService,
            IConfiguration configuration,
            ILogger<ApiOnlyAccessController> logger)
        {
            _apiOnlyAccessService = apiOnlyAccessService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves active API groups (submenus) from ERP_Submenus where is_active = 'Y'.
        /// Menu_id is mapped as Module_id; when Menu_id is 0, resolves Menu_id via parent_id -> Submenu_id.
        /// </summary>
        [HttpGet]
        [HttpGet("groups")]
        [HttpGet("submenus")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiGroupDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ApiGroupDto>>>> GetApiGroups(
            [FromQuery] int? moduleId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var groups = await _apiOnlyAccessService.GetApiGroupsAsync(cancellationToken);
                if (moduleId.HasValue && moduleId.Value > 0)
                {
                    groups = groups.Where(g => g.ModuleId == moduleId.Value);
                }
                return Ok(ApiResponse<IEnumerable<ApiGroupDto>>.SuccessResponse(groups));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<ApiGroupDto>>.FailureResponse(
                    "An error occurred while fetching API groups from ERP_Submenus.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Retrieves active HTTP methods from Prowess_ApiMethod where is_active = 'Y'.
        /// </summary>
        [HttpGet("methods")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ApiMethodDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ApiMethodDto>>>> GetApiMethods(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var methods = await _apiOnlyAccessService.GetApiMethodsAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<ApiMethodDto>>.SuccessResponse(methods));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<ApiMethodDto>>.FailureResponse(
                    "An error occurred while fetching API methods from Prowess_ApiMethod.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Retrieves all active API records from prowess_api_master.
        /// </summary>
        [HttpGet("apis")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProwessApiMasterDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ProwessApiMasterDto>>>> GetApis(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var apis = await _apiOnlyAccessService.GetApisAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<ProwessApiMasterDto>>.SuccessResponse(apis));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<ProwessApiMasterDto>>.FailureResponse(
                    "An error occurred while fetching APIs from prowess_api_master.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Saves a new API into prowess_api_master.
        /// </summary>
        [HttpPost("apis")]
        [HttpPost("save")]
        [ProducesResponseType(typeof(ApiResponse<ProwessApiMasterDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<ProwessApiMasterDto>>> CreateApi(
            [FromBody] SaveProwessApiRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                int userId = GetCurrentUserId();
                if (request.ApiMasterId.HasValue && request.ApiMasterId.Value > 0)
                {
                    var updated = await _apiOnlyAccessService.UpdateApiAsync(request.ApiMasterId.Value, request, userId, cancellationToken);
                    if (updated == null)
                    {
                        return NotFound(ApiResponse<ProwessApiMasterDto>.FailureResponse("API record not found."));
                    }
                    return Ok(ApiResponse<ProwessApiMasterDto>.SuccessResponse(updated, "API updated successfully."));
                }

                var created = await _apiOnlyAccessService.CreateApiAsync(request, userId, cancellationToken);
                return Ok(ApiResponse<ProwessApiMasterDto>.SuccessResponse(created, "API saved successfully."));
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return Conflict(ApiResponse<ProwessApiMasterDto>.FailureResponse(
                    $"API code '{request.ApiCode}' already exists.",
                    new List<string> { ex.Message }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ProwessApiMasterDto>.FailureResponse(
                    "An error occurred while saving the API.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Updates an existing API in prowess_api_master.
        /// </summary>
        [HttpPut("apis/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<ProwessApiMasterDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<ProwessApiMasterDto>>> UpdateApi(
            int id,
            [FromBody] SaveProwessApiRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                int userId = GetCurrentUserId();
                var updated = await _apiOnlyAccessService.UpdateApiAsync(id, request, userId, cancellationToken);
                if (updated == null)
                {
                    return NotFound(ApiResponse<ProwessApiMasterDto>.FailureResponse("API record not found."));
                }
                return Ok(ApiResponse<ProwessApiMasterDto>.SuccessResponse(updated, "API updated successfully."));
            }
            catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
            {
                return Conflict(ApiResponse<ProwessApiMasterDto>.FailureResponse(
                    $"API code '{request.ApiCode}' already exists.",
                    new List<string> { ex.Message }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ProwessApiMasterDto>.FailureResponse(
                    "An error occurred while updating the API.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Soft-deletes an API in prowess_api_master.
        /// </summary>
        [HttpDelete("apis/{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteApi(
            int id,
            CancellationToken cancellationToken = default)
        {
            try
            {
                int userId = GetCurrentUserId();
                var deleted = await _apiOnlyAccessService.DeleteApiAsync(id, userId, cancellationToken);
                return Ok(ApiResponse<bool>.SuccessResponse(deleted, "API deleted successfully."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.FailureResponse(
                    "An error occurred while deleting the API.",
                    new List<string> { ex.Message }));
            }
        }

        private int GetCurrentUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(idClaim, out int uid) && uid > 0 ? uid : 1;
        }

        [HttpGet("get_client_master")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<client_master_dto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<client_master_dto>>>> get_client_master(
            CancellationToken cancellationToken = default)
        {
            try
            {
                var list = await _apiOnlyAccessService.get_client_master(cancellationToken);
                return Ok(ApiResponse<IEnumerable<client_master_dto>>.SuccessResponse(list));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<client_master_dto>>.FailureResponse(
                    "An error occurred while fetching clients from Client_Master.",
                    new List<string> { ex.Message }));
            }
        }

        [HttpGet("ApiCustomerMapping")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<api_customer_mapping_dto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<api_customer_mapping_dto>>>> GetApiCustomerMapping(
            [FromQuery] int? clientId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var list = await _apiOnlyAccessService.get_customer_mappings(clientId, cancellationToken);
                return Ok(ApiResponse<IEnumerable<api_customer_mapping_dto>>.SuccessResponse(list));
            }
            catch (OperationCanceledException)
            {
                // Client cancelled or disconnected (e.g. browser refreshed or navigated away)
                return StatusCode(StatusCodes.Status499ClientClosedRequest, ApiResponse<IEnumerable<api_customer_mapping_dto>>.FailureResponse("Request was cancelled by client."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<api_customer_mapping_dto>>.FailureResponse(
                    "An error occurred while fetching customer mappings.",
                    new List<string> { ex.Message }));
            }
        }

        [HttpPost("ApiCustomerMapping")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<bool>>> PostApiCustomerMapping(
            [FromBody] save_customer_mappings_request request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (request.client_id <= 0)
                {
                    return BadRequest(ApiResponse<bool>.FailureResponse("Valid client_id is required."));
                }

                int userId = GetCurrentUserId();
                bool saved = await _apiOnlyAccessService.save_customer_mappings(
                    request.client_id,
                    request.client_name,
                    request.mappings,
                    userId,
                    cancellationToken);

                return Ok(ApiResponse<bool>.SuccessResponse(saved, "Customer API mappings saved successfully."));
            }
            catch (OperationCanceledException)
            {
                return StatusCode(StatusCodes.Status499ClientClosedRequest, ApiResponse<bool>.FailureResponse("Request was cancelled by client."));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, ApiResponse<bool>.FailureResponse(
                    ex.Message,
                    new List<string> { ex.Message }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.FailureResponse(
                    "An error occurred while saving customer mappings.",
                    new List<string> { ex.Message }));
            }
        }

        [HttpGet("rollback-logs")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProwessRollbackLogDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ProwessRollbackLogDto>>>> GetRollbackLogs(
            [FromQuery] int? clientId = null,
            [FromQuery] int limit = 100,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var logs = await _apiOnlyAccessService.GetRollbackLogsAsync(clientId, limit, cancellationToken);
                return Ok(ApiResponse<IEnumerable<ProwessRollbackLogDto>>.SuccessResponse(logs));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<ProwessRollbackLogDto>>.FailureResponse(
                    "An error occurred while fetching rollback logs.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Receives failed external API call logs from ProwessBackend, writes them to company-wise log files
        /// and persists them in dbo.Prowess_Api_Error_Log.
        /// </summary>
        [HttpPost("error-logs")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<ApiErrorLogIngestResultDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<ApiErrorLogIngestResultDto>>> IngestApiErrorLogs(
            [FromBody] ApiErrorLogBatchRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string? configuredKey = _configuration["CrmSync:ApiKey"];
                if (!string.IsNullOrWhiteSpace(configuredKey))
                {
                    string providedKey = Request.Headers["X-CRM-Sync-Key"].ToString();
                    if (!string.Equals(configuredKey.Trim(), providedKey.Trim(), StringComparison.Ordinal))
                    {
                        return Unauthorized(ApiResponse<ApiErrorLogIngestResultDto>.FailureResponse("Invalid X-CRM-Sync-Key."));
                    }
                }

                if (request == null || request.Logs == null || request.Logs.Count == 0)
                {
                    return BadRequest(ApiResponse<ApiErrorLogIngestResultDto>.FailureResponse("No error log entries provided."));
                }

                var result = await _apiOnlyAccessService.IngestApiErrorLogsAsync(request, cancellationToken);
                _logger.LogInformation(
                    "Ingested {InsertedCount} API error log(s) for ClientId={ClientId}. Files={Files}",
                    result.InsertedCount,
                    request.ClientId,
                    string.Join(", ", result.LogFiles));
                return Ok(ApiResponse<ApiErrorLogIngestResultDto>.SuccessResponse(result, "API error logs recorded."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record API error logs for ClientId={ClientId}", request?.ClientId);

                return StatusCode(500, ApiResponse<ApiErrorLogIngestResultDto>.FailureResponse(
                    "An error occurred while recording API error logs.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Retrieves company-wise failed external API logs with filtering, summary KPIs, and pagination.
        /// </summary>
        [HttpGet("error-logs")]
        [ProducesResponseType(typeof(ApiResponse<ApiErrorLogQueryResultDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<ApiErrorLogQueryResultDto>>> GetApiErrorLogs(
            [FromQuery] int? clientId = null,
            [FromQuery] string? environment = null,
            [FromQuery] string? statusFilter = null,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _apiOnlyAccessService.GetApiErrorLogsAsync(
                    clientId, environment, statusFilter, search, fromDate, toDate, page, pageSize, cancellationToken);
                return Ok(ApiResponse<ApiErrorLogQueryResultDto>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ApiErrorLogQueryResultDto>.FailureResponse(
                    "An error occurred while fetching API error logs.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Lists physical company-wise .log files stored on the CRM server.
        /// </summary>
        [HttpGet("error-log-files")]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<CompanyLogFileDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<CompanyLogFileDto>>>> GetCompanyLogFiles(
            [FromQuery] int? clientId = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var files = await _apiOnlyAccessService.GetCompanyLogFilesAsync(clientId, cancellationToken);
                return Ok(ApiResponse<IEnumerable<CompanyLogFileDto>>.SuccessResponse(files));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<CompanyLogFileDto>>.FailureResponse(
                    "An error occurred while listing company log files.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Reads the contents of a company-wise .log file for display in CRM.
        /// </summary>
        [HttpGet("error-log-files/content")]
        [ProducesResponseType(typeof(ApiResponse<CompanyLogFileContentDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<CompanyLogFileContentDto>>> GetCompanyLogFileContent(
            [FromQuery] int? clientId,
            [FromQuery] string fileName,
            [FromQuery] int tailLines = 500,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var content = await _apiOnlyAccessService.ReadCompanyLogFileAsync(clientId, fileName, tailLines, cancellationToken);
                if (content == null)
                {
                    return NotFound(ApiResponse<CompanyLogFileContentDto>.FailureResponse("Log file not found."));
                }
                return Ok(ApiResponse<CompanyLogFileContentDto>.SuccessResponse(content));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<CompanyLogFileContentDto>.FailureResponse(
                    "An error occurred while reading the company log file.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Downloads a company-wise .log file as text/plain.
        /// </summary>
        [HttpGet("error-log-files/download")]
        public async Task<IActionResult> DownloadCompanyLogFile(
            [FromQuery] int? clientId,
            [FromQuery] string fileName,
            CancellationToken cancellationToken = default)
        {
            var fileResult = await _apiOnlyAccessService.ReadCompanyLogFileBytesAsync(clientId, fileName, cancellationToken);
            if (fileResult == null)
            {
                return NotFound(ApiResponse<string>.FailureResponse("Log file not found."));
            }

            return File(fileResult.Value.Bytes, "text/plain; charset=utf-8", fileResult.Value.FileName);
        }



        /// <summary>
        /// Receives aggregated monthly API usage totals per customer from ProwessBackend.
        /// </summary>
        [HttpPost("usage-sync")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<int>>> SyncCustomerApiUsage(
            [FromBody] CustomerApiUsageSyncRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                string? configuredKey = _configuration["CrmSync:ApiKey"];
                if (!string.IsNullOrWhiteSpace(configuredKey))
                {
                    string providedKey = Request.Headers["X-CRM-Sync-Key"].ToString();
                    if (!string.Equals(configuredKey.Trim(), providedKey.Trim(), StringComparison.Ordinal))
                    {
                        return Unauthorized(ApiResponse<int>.FailureResponse("Invalid X-CRM-Sync-Key."));
                    }
                }

                if (request == null || request.ClientId <= 0)
                {
                    return BadRequest(ApiResponse<int>.FailureResponse("Valid client_id is required."));
                }

                int saved = await _apiOnlyAccessService.UpsertCustomerApiUsageAsync(request, cancellationToken);
                _logger.LogInformation("Synced {Count} customer API usage row(s) for ClientId={ClientId}.", saved, request.ClientId);
                return Ok(ApiResponse<int>.SuccessResponse(saved, "Customer API monthly usage synced successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync customer API usage for ClientId={ClientId}", request?.ClientId);
                return StatusCode(500, ApiResponse<int>.FailureResponse(
                    "An error occurred while syncing customer API usage.",
                    new List<string> { ex.Message }));
            }
        }

        /// <summary>
        /// Retrieves customer-wise monthly API usage totals (aggregated across all users) vs licensed Monthly_Quota.
        /// </summary>
        [HttpGet("customer-usage")]
        [ProducesResponseType(typeof(ApiResponse<CustomerApiUsageQueryResultDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<CustomerApiUsageQueryResultDto>>> GetCustomerApiUsage(
            [FromQuery] int? clientId = null,
            [FromQuery] int? usageMonth = null,
            [FromQuery] string? search = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _apiOnlyAccessService.GetCustomerApiUsageAsync(clientId, usageMonth, search, cancellationToken);
                return Ok(ApiResponse<CustomerApiUsageQueryResultDto>.SuccessResponse(result));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<CustomerApiUsageQueryResultDto>.FailureResponse(
                    "An error occurred while fetching customer API usage.",
                    new List<string> { ex.Message }));
            }
        }



    }
}
