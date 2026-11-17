using Microsoft.AspNetCore.Mvc;
using Payroll_CRM_API.Models;
using Payroll_CRM_API.Services;

namespace Payroll_CRM_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ESModuleController : ControllerBase
    {
        private readonly IESModuleService _esModuleService;

        public ESModuleController(IESModuleService esModuleService)
        {
            _esModuleService = esModuleService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<ESModuleMaster>>>> GetAllESModules(CancellationToken cancellationToken)
        {
            try
            {
                var modules = await _esModuleService.GetAllESModulesAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<ESModuleMaster>>.SuccessResponse(modules));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<ESModuleMaster>>.FailureResponse("An error occurred while fetching ESPortal modules.", new List<string> { ex.Message }));
            }
        }
    }
}
