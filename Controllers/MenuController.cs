using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROWESS_CRM.Models;
using PROWESS_CRM.Services;

namespace PROWESS_CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MenuController : ControllerBase
    {
        private readonly IMenuService _menuService;

        public MenuController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        /// <summary>
        /// Retrieves permitted sidebar navigation menus for the logged-in user's role and selected product suite
        /// </summary>
        /// <param name="productCode">Optional product code filter (e.g. 'HRMS', 'PAYROLL', 'CRM'). Common menus are always returned.</param>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<MenuMaster>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetUserMenus([FromQuery] string? productCode = null)
        {
            // Extract role from JWT claim
            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

            var menus = await _menuService.GetMenusForRoleAsync(roleClaim, productCode);
            return Ok(menus);
        }
    }
}
