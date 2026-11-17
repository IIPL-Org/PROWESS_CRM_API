using Microsoft.AspNetCore.Mvc;
using PROWESS_CRM.Models;
using PROWESS_CRM.Services;

namespace PROWESS_CRM.Controllers
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
