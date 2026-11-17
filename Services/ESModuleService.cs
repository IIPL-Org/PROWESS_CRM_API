using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IESModuleService
    {
        Task<IEnumerable<ESModuleMaster>> GetAllESModulesAsync(CancellationToken cancellationToken = default);
    }

    public class ESModuleService : IESModuleService
    {
        private readonly IESModuleRepository _esModuleRepository; //

        public ESModuleService(IESModuleRepository esModuleRepository)
        {
            _esModuleRepository = esModuleRepository;
        }

        public async Task<IEnumerable<ESModuleMaster>> GetAllESModulesAsync(CancellationToken cancellationToken = default)
        {
            return await _esModuleRepository.GetAllESModulesAsync(cancellationToken);
        }
    }
}
