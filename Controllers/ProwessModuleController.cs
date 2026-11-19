using Microsoft.AspNetCore.Mvc;
using PROWESS_CRM.Models;
using PROWESS_CRM.Services;

namespace PROWESS_CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class ProwessModuleController : ControllerBase
    {
        private readonly IProwessModuleService _prowessModuleService;

        public ProwessModuleController(IProwessModuleService prowessModuleService)
        {
            _prowessModuleService = prowessModuleService;
        }

        /// <summary>
        /// Retrieves all active Prowess ERP modules from Prowess_Module_Master.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProwessModuleMaster>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResponse<IEnumerable<ProwessModuleMaster>>>> GetAllProwessModules(CancellationToken cancellationToken)
        {
            try
            {
                var modules = await _prowessModuleService.GetAllProwessModulesAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<ProwessModuleMaster>>.SuccessResponse(modules));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<ProwessModuleMaster>>.FailureResponse(
                    "An error occurred while fetching Prowess modules.",
                    new List<string> { ex.Message }));
            }
        }
    }
}

