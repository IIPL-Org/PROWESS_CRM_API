using Payroll_CRM_API.Data;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Services
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
