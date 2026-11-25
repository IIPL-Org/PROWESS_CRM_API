using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
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

        public ApiOnlyAccessController(IApiOnlyAccessService apiOnlyAccessService)
        {
            _apiOnlyAccessService = apiOnlyAccessService;
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
    }
}
