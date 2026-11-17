using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
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
