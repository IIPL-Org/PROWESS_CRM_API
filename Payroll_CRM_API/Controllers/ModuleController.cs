using Microsoft.AspNetCore.Mvc;
using Payroll_CRM_API.Models;
using Payroll_CRM_API.Services;

namespace Payroll_CRM_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModuleController : ControllerBase
    {
        private readonly IModuleService _moduleService;

        public ModuleController(IModuleService moduleService)
        {
            _moduleService = moduleService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<ModuleMaster>>>> GetAllModules(CancellationToken cancellationToken)
        {
            try
            {
                var modules = await _moduleService.GetAllModulesAsync(cancellationToken);
                return Ok(ApiResponse<IEnumerable<ModuleMaster>>.SuccessResponse(modules));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<IEnumerable<ModuleMaster>>.FailureResponse("An error occurred while fetching modules.", new List<string> { ex.Message }));
            }
        }
    }
}
