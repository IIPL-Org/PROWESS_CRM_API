using PROWESS_CRM.Data;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Services
{
    public interface IClientService
    {
        Task<ClientResponseDto> CreateClientAsync(CreateClientDto dto, CancellationToken cancellationToken = default);
        Task<bool> UpdateClientAsync(UpdateClientDto dto, CancellationToken cancellationToken = default);
        Task<ClientResponseDto?> GetClientByIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClientResponseDto>> GetAllClientsAsync(CancellationToken cancellationToken = default);
        Task<bool> IsClientNameExistsAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default);
        Task<bool> IsActiveClientNameExistsAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default);
        Task<bool> AssignModulesAsync(AssignModulesDto dto, CancellationToken cancellationToken = default);
        Task<bool> AssignESModulesAsync(AssignESModulesDto dto, CancellationToken cancellationToken = default);
        Task<bool> AssignProwessModulesAsync(AssignProwessModulesDto dto, CancellationToken cancellationToken = default);

    }

    public class ClientService : IClientService
    {
        private readonly IClientRepository _clientRepository;
        private readonly IModuleRepository _moduleRepository;
        private readonly IESModuleRepository _esModuleRepository;
        private readonly IHttpClientFactory _httpClientFactory;

        public ClientService(IClientRepository clientRepository, IModuleRepository moduleRepository, IESModuleRepository esModuleRepository, IHttpClientFactory httpClientFactory)
        {
            _clientRepository = clientRepository;
            _moduleRepository = moduleRepository;
            _esModuleRepository = esModuleRepository;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ClientResponseDto> CreateClientAsync(CreateClientDto dto, CancellationToken cancellationToken = default)
        {
            char isActiveChar = char.ToUpper(dto.IsActive);
            if (isActiveChar == 'Y')
            {
                if (await _clientRepository.IsActiveClientNameExistsAsync(dto.ClientName, null, cancellationToken))
                {
                    throw new InvalidOperationException($"An active client with name '{dto.ClientName.Trim()}' already exists.");
                }
            }

            var client = new ClientMaster
            {
                ClientName = dto.ClientName.Trim(),
                ClientAddress = dto.ClientAddress?.Trim(),
                HrmsAdminLink = dto.HrmsAdminLink?.Trim(),
                HrmsApiLink = dto.HrmsApiLink?.Trim(),
                HrmsEssLink = dto.HrmsEssLink?.Trim(),
                HrmsAdminAccess = char.ToUpper(dto.HrmsAdminAccess),
                HrmsApiAccess = char.ToUpper(dto.HrmsApiAccess),
                HrmsEssAccess = char.ToUpper(dto.HrmsEssAccess),
                HrmsAppAccess = char.ToUpper(dto.HrmsAppAccess),
                IsActive = isActiveChar,
                CreatedBy = dto.CreatedBy,
                CreatedDate = DateTime.Now
            };

            int newClientId = await _clientRepository.CreateClientAsync(client, cancellationToken);
            var createdClient = await _clientRepository.GetClientByIdAsync(newClientId, cancellationToken);

            if (createdClient == null)
            {
                throw new Exception("Failed to retrieve created client record.");
            }

            return MapToResponseDto(createdClient);
        }

        public async Task<bool> UpdateClientAsync(UpdateClientDto dto, CancellationToken cancellationToken = default)
        {
            var existingClient = await _clientRepository.GetClientByIdAsync(dto.ClientId, cancellationToken);
            if (existingClient == null)
            {
                return false;
            }

            char isActiveChar = char.ToUpper(dto.IsActive);
            if (isActiveChar == 'Y')
            {
                if (await _clientRepository.IsActiveClientNameExistsAsync(dto.ClientName, dto.ClientId, cancellationToken))
                {
                    throw new InvalidOperationException($"An active client with name '{dto.ClientName.Trim()}' already exists.");
                }
            }

            existingClient.ClientName = dto.ClientName.Trim();
            existingClient.ClientAddress = dto.ClientAddress?.Trim();
            existingClient.HrmsAdminLink = dto.HrmsAdminLink?.Trim();
            existingClient.HrmsApiLink = dto.HrmsApiLink?.Trim();
            existingClient.HrmsEssLink = dto.HrmsEssLink?.Trim();
            existingClient.HrmsAdminAccess = char.ToUpper(dto.HrmsAdminAccess);
            existingClient.HrmsApiAccess = char.ToUpper(dto.HrmsApiAccess);
            existingClient.HrmsEssAccess = char.ToUpper(dto.HrmsEssAccess);
            existingClient.HrmsAppAccess = char.ToUpper(dto.HrmsAppAccess);
            existingClient.IsActive = isActiveChar;
            existingClient.ModifiedBy = dto.ModifiedBy;
            existingClient.ModifiedDate = DateTime.Now;

            return await _clientRepository.UpdateClientAsync(existingClient, cancellationToken);
        }

        public async Task<bool> AssignModulesAsync(AssignModulesDto dto, CancellationToken cancellationToken = default)
        {
            var existingClient = await _clientRepository.GetClientByIdAsync(dto.ClientId, cancellationToken);
            if (existingClient == null)
            {
                return false;
            }

            // 1. Save locally in CRM database
            await _clientRepository.SaveClientModulesAsync(dto.ClientId, dto.ModuleIds ?? new List<int>(), cancellationToken);

            // 2. Fetch full module details
            var allModules = await _moduleRepository.GetAllModulesAsync(cancellationToken);
            var assignedModuleIds = dto.ModuleIds ?? new List<int>();
            var assignedModules = allModules.Where(m => assignedModuleIds.Contains(m.ModuleId)).ToList();

            // 3. Push to Client's DB
            if (!string.IsNullOrWhiteSpace(existingClient.HrmsApiLink))
            {
                try
                {
                    string baseUrl = existingClient.HrmsApiLink.TrimEnd('/');
                    string syncUrl = $"{baseUrl}/api/CrmSync/sync-client-modules";

                    var payload = assignedModules.Select(m => new
                    {
                        ModuleId = m.ModuleId,
                        ModuleName = m.ModuleName,
                        ShortName = m.ShortName,
                        Description = m.Description
                    }).ToList();

                    var httpClient = _httpClientFactory.CreateClient();
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        System.Text.Encoding.UTF8,
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                    var response = await httpClient.PostAsync(syncUrl, content, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync(cancellationToken);
                        Console.WriteLine($"Failed to push modules to client {dto.ClientId}. Status: {response.StatusCode}. Error: {err}");
                        // Optionally throw or log, but for now we won't block the CRM save.
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while pushing modules to client {dto.ClientId}: {ex.Message}");
                }
            }

            return true;
        }

        public async Task<bool> AssignESModulesAsync(AssignESModulesDto dto, CancellationToken cancellationToken = default)
        {
            var existingClient = await _clientRepository.GetClientByIdAsync(dto.ClientId, cancellationToken);
            if (existingClient == null)
            {
                return false;
            }

            // 1. Save locally in CRM database
            await _clientRepository.SaveClientESModulesAsync(dto.ClientId, dto.ModuleIds ?? new List<int>(), cancellationToken);

            // 2. Fetch full module details
            var allModules = await _esModuleRepository.GetAllESModulesAsync(cancellationToken);
            var assignedModuleIds = dto.ModuleIds ?? new List<int>();
            var assignedModules = allModules.Where(m => assignedModuleIds.Contains(m.ModuleId)).ToList();

            // 3. Push to Client's DB
            if (!string.IsNullOrWhiteSpace(existingClient.HrmsApiLink))
            {
                try
                {
                    string baseUrl = existingClient.HrmsApiLink.TrimEnd('/');
                    string syncUrl = $"{baseUrl}/api/CrmSync/sync-client-es-modules";

                    var payload = assignedModules.Select(m => new
                    {
                        ModuleId = m.ModuleId,
                        ModuleName = m.ModuleName,
                        ShortName = m.ShortName,
                        Description = m.Description
                    }).ToList();

                    var httpClient = _httpClientFactory.CreateClient();
                    var content = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(payload),
                        System.Text.Encoding.UTF8,
                        new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));

                    var response = await httpClient.PostAsync(syncUrl, content, cancellationToken);
                    if (!response.IsSuccessStatusCode)
                    {
                        string err = await response.Content.ReadAsStringAsync(cancellationToken);
                        Console.WriteLine($"Failed to push ES modules to client {dto.ClientId}. Status: {response.StatusCode}. Error: {err}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception while pushing ES modules to client {dto.ClientId}: {ex.Message}");
                }
            }

            return true;
        }

        public async Task<bool> AssignProwessModulesAsync(AssignProwessModulesDto dto, CancellationToken cancellationToken = default)
        {
            var existingClient = await _clientRepository.GetClientByIdAsync(dto.ClientId, cancellationToken);
            if (existingClient == null)
            {
                return false;
            }

            await _clientRepository.SaveClientProwessModulesAsync(dto.ClientId, dto.ModuleIds ?? new List<int>(), cancellationToken);
            return true;
        }

        public async Task<ClientResponseDto?> GetClientByIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            var client = await _clientRepository.GetClientByIdAsync(clientId, cancellationToken);
            return client != null ? MapToResponseDto(client) : null;
        }

        public async Task<IEnumerable<ClientResponseDto>> GetAllClientsAsync(CancellationToken cancellationToken = default)
        {
            var clients = await _clientRepository.GetAllClientsAsync(cancellationToken);
            return clients.Select(MapToResponseDto);
        }

        public async Task<bool> IsClientNameExistsAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default)
        {
            return await _clientRepository.ClientExistsByNameAsync(clientName, excludeClientId, cancellationToken);
        }

        public async Task<bool> IsActiveClientNameExistsAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default)
        {
            return await _clientRepository.IsActiveClientNameExistsAsync(clientName, excludeClientId, cancellationToken);
        }

        private static ClientResponseDto MapToResponseDto(ClientMaster client)
        {
            return new ClientResponseDto
            {
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                ClientAddress = client.ClientAddress,
                HrmsAdminLink = client.HrmsAdminLink,
                HrmsApiLink = client.HrmsApiLink,
                HrmsEssLink = client.HrmsEssLink,
                HrmsAdminAccess = client.HrmsAdminAccess,
                HrmsApiAccess = client.HrmsApiAccess,
                HrmsEssAccess = client.HrmsEssAccess,
                HrmsAppAccess = client.HrmsAppAccess,
                IsActive = client.IsActive,
                CreatedBy = client.CreatedBy,
                CreatedDate = client.CreatedDate,
                ModifiedBy = client.ModifiedBy,
                ModifiedDate = client.ModifiedDate,
                ModuleIds = client.ModuleIds ?? new List<int>(),
                EsModuleIds = client.EsModuleIds ?? new List<int>(),
                ProwessModuleIds = client.ProwessModuleIds ?? new List<int>()
            };
        }
    }
}
