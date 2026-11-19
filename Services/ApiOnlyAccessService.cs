using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IApiOnlyAccessService
    {
        Task<IEnumerable<ApiGroupDto>> GetApiGroupsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ApiMethodDto>> GetApiMethodsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ProwessApiMasterDto>> GetApisAsync(CancellationToken cancellationToken = default);
        Task<ProwessApiMasterDto> CreateApiAsync(SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default);
        Task<ProwessApiMasterDto?> UpdateApiAsync(int apiMasterId, SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteApiAsync(int apiMasterId, int userId, CancellationToken cancellationToken = default);
    }

    public class ApiOnlyAccessService : IApiOnlyAccessService
    {
        private readonly IApiOnlyAccessRepository _apiOnlyAccessRepository;

        public ApiOnlyAccessService(IApiOnlyAccessRepository apiOnlyAccessRepository)
        {
            _apiOnlyAccessRepository = apiOnlyAccessRepository;
        }

        public async Task<IEnumerable<ApiGroupDto>> GetApiGroupsAsync(CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.GetApiGroupsAsync(cancellationToken);
        }

        public async Task<IEnumerable<ApiMethodDto>> GetApiMethodsAsync(CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.GetApiMethodsAsync(cancellationToken);
        }

        public async Task<IEnumerable<ProwessApiMasterDto>> GetApisAsync(CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.GetApisAsync(cancellationToken);
        }

        public async Task<ProwessApiMasterDto> CreateApiAsync(SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.CreateApiAsync(request, userId, cancellationToken);
        }

        public async Task<ProwessApiMasterDto?> UpdateApiAsync(int apiMasterId, SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.UpdateApiAsync(apiMasterId, request, userId, cancellationToken);
        }

        public async Task<bool> DeleteApiAsync(int apiMasterId, int userId, CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.DeleteApiAsync(apiMasterId, userId, cancellationToken);
        }
    }
}
