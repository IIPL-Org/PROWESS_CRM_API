using Payroll_CRM_API.Data;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Services
{
    public interface IESModuleService
    {
        Task<IEnumerable<ESModuleMaster>> GetAllESModulesAsync(CancellationToken cancellationToken = default);
    }

    public class ESModuleService : IESModuleService
    {
        private readonly IESModuleRepository _esModuleRepository;

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
