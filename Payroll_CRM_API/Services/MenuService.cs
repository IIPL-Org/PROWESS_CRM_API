using Payroll_CRM_API.Data;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Services
{
    public interface IMenuService
    {
        Task<IEnumerable<MenuMaster>> GetMenusForRoleAsync(string roleName, string? productCode = null);
    }

    public class MenuService : IMenuService
    {
        private readonly IMenuRepository _menuRepository;

        public MenuService(IMenuRepository menuRepository)
        {
            _menuRepository = menuRepository;
        }

        public async Task<IEnumerable<MenuMaster>> GetMenusForRoleAsync(string roleName, string? productCode = null)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return await _menuRepository.GetAllMenusAsync(productCode);
            }

            return await _menuRepository.GetMenusByRoleAsync(roleName, productCode);
        }
    }
}
