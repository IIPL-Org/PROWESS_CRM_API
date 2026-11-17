using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IModuleService
    {
        Task<IEnumerable<ModuleMaster>> GetAllModulesAsync(CancellationToken cancellationToken = default);
    }

    public class ModuleService : IModuleService
    {
        private readonly IModuleRepository _moduleRepository;

        public ModuleService(IModuleRepository moduleRepository)
        {
            _moduleRepository = moduleRepository;
        }

        public async Task<IEnumerable<ModuleMaster>> GetAllModulesAsync(CancellationToken cancellationToken = default)
        {
            return await _moduleRepository.GetAllModulesAsync(cancellationToken);
        }
    }
}
