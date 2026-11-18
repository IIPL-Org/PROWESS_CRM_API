using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IProwessModuleService
    {
        Task<IEnumerable<ProwessModuleMaster>> GetAllProwessModulesAsync(CancellationToken cancellationToken = default);
    }

    public class ProwessModuleService : IProwessModuleService
    {
        private readonly IProwessModuleRepository _prowessModuleRepository;

        public ProwessModuleService(IProwessModuleRepository prowessModuleRepository)
        {
            _prowessModuleRepository = prowessModuleRepository;
        }

        public async Task<IEnumerable<ProwessModuleMaster>> GetAllProwessModulesAsync(CancellationToken cancellationToken = default)
        {
            return await _prowessModuleRepository.GetAllProwessModulesAsync(cancellationToken);
        }
    }
}

