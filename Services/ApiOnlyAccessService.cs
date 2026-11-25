using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
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
        Task<IEnumerable<client_master_dto>> get_client_master(CancellationToken cancellationToken = default);
        Task<IEnumerable<api_customer_mapping_dto>> get_customer_mappings(int? clientId = null, CancellationToken cancellationToken = default);
        Task<bool> save_customer_mappings(int clientId, string? clientName, IEnumerable<api_customer_mapping_dto> mappings, int userId, CancellationToken cancellationToken = default);
    }

    public class ApiOnlyAccessService : IApiOnlyAccessService
    {
        private readonly IApiOnlyAccessRepository _apiOnlyAccessRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApiOnlyAccessService> _logger;

        public ApiOnlyAccessService(
            IApiOnlyAccessRepository apiOnlyAccessRepository,
            IHttpClientFactory httpClientFactory,
            ILogger<ApiOnlyAccessService> logger)
        {
            _apiOnlyAccessRepository = apiOnlyAccessRepository;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
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

        public async Task<IEnumerable<client_master_dto>> get_client_master(CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.get_client_master(cancellationToken);
        }

        public async Task<IEnumerable<api_customer_mapping_dto>> get_customer_mappings(int? clientId = null, CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.get_customer_mappings(clientId, cancellationToken);
        }

        public async Task<bool> save_customer_mappings(int clientId, string? clientName, IEnumerable<api_customer_mapping_dto> mappings, int userId, CancellationToken cancellationToken = default)
        {
            var mappingList = mappings?.ToList() ?? new List<api_customer_mapping_dto>();

            // Delegate to sync to client's HRMS database BEFORE committing local CRM database
            Func<Task<(bool Success, string Error)>> onBeforeCommit = async () =>
            {
                return await SyncToClientHrmsAsync(clientId, clientName, mappingList, cancellationToken);
            };

            return await _apiOnlyAccessRepository.save_customer_mappings(
                clientId,
                clientName,
                mappingList,
                userId,
                onBeforeCommit,
                cancellationToken);
        }

        private async Task<(bool Success, string Error)> SyncToClientHrmsAsync(int clientId, string? clientName, List<api_customer_mapping_dto> mappings, CancellationToken cancellationToken)
        {
            try
            {
                // Retrieve client's HRMS_API_Link from dbo.Client_Master
                string? apiLink = await _apiOnlyAccessRepository.get_client_hrms_link(clientId, cancellationToken);
                if (string.IsNullOrWhiteSpace(apiLink))
                {
                    string msg = $"No HRMS_API_Link configured in Client_Master for Client_Id: {clientId}";
                    Console.WriteLine($"[CRM Sync] ⚠️ {msg}");
                    _logger.LogWarning(msg);
                    return (false, msg);
                }

                string rawLink = apiLink.Trim().TrimEnd('/');
                var candidateBaseUrls = new List<string>();

                if (rawLink.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    rawLink.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    candidateBaseUrls.Add(rawLink);
                    // If https://localhost..., also try http://localhost...
                    if (rawLink.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase) ||
                        rawLink.StartsWith("https://127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    {
                        candidateBaseUrls.Add("http://" + rawLink.Substring(8));
                    }
                }
                else
                {
                    // No scheme specified
                    if (rawLink.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                        rawLink.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    {
                        // If user entered just "localhost" or "localhost:5126"
                        string host = rawLink;
                        if (!host.Contains(':'))
                        {
                            host += ":5126"; // Default port for Prowess-Backend
                        }
                        candidateBaseUrls.Add($"http://{host}");
                        candidateBaseUrls.Add($"https://{host}");
                    }
                    else
                    {
                        candidateBaseUrls.Add($"https://{rawLink}");
                        candidateBaseUrls.Add($"http://{rawLink}");
                    }
                }

                // Enrich mappings with API master details (method_id, method_name, endpoint_path, api_name, rights_action, summary)
                var allApis = await _apiOnlyAccessRepository.GetApisAsync(cancellationToken);
                var apiMap = allApis.ToDictionary(a => a.ApiMasterId);

                var enrichedMappings = mappings.Select(m =>
                {
                    apiMap.TryGetValue(m.api_master_id, out var api);
                    return new
                    {
                        api_customer_mapping_id = m.api_customer_mapping_id,
                        api_master_id = m.api_master_id,
                        api_code = m.api_code,
                        method_id = api?.MethodId,
                        method_name = api?.MethodName,
                        endpoint_path = api?.EndpointPath,
                        api_name = api?.ApiName,
                        rights_action = api?.RightsAction,
                        summary = api?.Summary,
                        onward_exposure = m.onward_exposure,
                        consumer_types = m.consumer_types,
                        max_data_scope = m.max_data_scope,
                        sensitivity = m.sensitivity,
                        credentials = m.credentials,
                        max_rate_per_min = m.max_rate_per_min,
                        monthly_quota = m.monthly_quota,
                        require_ip_whitelist = m.require_ip_whitelist.ToString(),
                        require_idempotency_key = m.require_idempotency_key.ToString(),
                        sandbox_available = m.sandbox_available.ToString(),
                        valid_till = m.valid_till?.ToString("yyyy-MM-dd"),
                        is_active = m.is_active.ToString()
                    };
                }).ToList();

                var payload = new
                {
                    client_id = clientId,
                    client_name = clientName,
                    mappings = enrichedMappings
                };

                // Use handler that tolerates local dev certificates
                using var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                };
                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(15);

                string lastError = "Could not connect to client HRMS API.";
                bool synced = false;
                foreach (var baseUrl in candidateBaseUrls)
                {
                    if (synced) break;

                    string[] endpointPaths = new[]
                    {
                        "/api/CrmSync/sync-client-api-mappings",
                        "/ApisAvailable/save",
                        "/ApisAvailable/apis"
                    };

                    foreach (var path in endpointPaths)
                    {
                        string targetUrl = $"{baseUrl}{path}";
                        try
                        {
                            Console.WriteLine($"[CRM Sync] 🔄 Pushing {mappings.Count} API mappings for Client {clientId} to {targetUrl}...");
                            var response = await client.PostAsJsonAsync(targetUrl, payload, cancellationToken);

                            if (response.IsSuccessStatusCode)
                            {
                                string body = await response.Content.ReadAsStringAsync(cancellationToken);
                                try
                                {
                                    using var doc = System.Text.Json.JsonDocument.Parse(body);
                                    if (doc.RootElement.TryGetProperty("success", out var sProp) && !sProp.GetBoolean())
                                    {
                                        string err = doc.RootElement.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? "Sync failed" : "Sync failed";
                                        Console.WriteLine($"[CRM Sync] ⚠️ Remote API returned success=false: {err}");
                                        lastError = err;
                                        continue;
                                    }
                                }
                                catch { }

                                Console.WriteLine($"[CRM Sync] ✅ Successfully pushed API mappings to {targetUrl} (Status: {response.StatusCode})");
                                _logger.LogInformation("Successfully synced API mappings to {Url} for Client {ClientId}", targetUrl, clientId);
                                return (true, string.Empty);
                            }
                            else
                            {
                                string err = await response.Content.ReadAsStringAsync(cancellationToken);
                                Console.WriteLine($"[CRM Sync] ⚠️ Attempt to {targetUrl} failed with status {response.StatusCode}: {err}");
                                lastError = $"Status {(int)response.StatusCode}: {err}";
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[CRM Sync] ❌ Attempt to {targetUrl} encountered exception: {ex.Message}");
                            lastError = ex.Message;
                        }
                    }
                }

                _logger.LogWarning("Failed to push API mappings to client {ClientId} across all candidate URLs: {Urls}",
                    clientId, string.Join(", ", candidateBaseUrls));
                return (false, lastError);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CRM Sync] ❌ Fatal error during client sync: {ex.Message}");
                _logger.LogWarning(ex, "Could not sync customer mappings to Client {ClientId} HRMS link.", clientId);
                return (false, ex.Message);
            }
        }
    }
}
