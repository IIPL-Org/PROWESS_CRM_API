using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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
        Task<IEnumerable<ProwessRollbackLogDto>> GetRollbackLogsAsync(int? clientId = null, int limit = 100, CancellationToken cancellationToken = default);
        Task<ApiErrorLogIngestResultDto> IngestApiErrorLogsAsync(ApiErrorLogBatchRequest request, CancellationToken cancellationToken = default);
        Task<ApiErrorLogQueryResultDto> GetApiErrorLogsAsync(int? clientId = null, string? environment = null, string? statusFilter = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<CompanyLogFileDto>> GetCompanyLogFilesAsync(int? clientId = null, CancellationToken cancellationToken = default);
        Task<CompanyLogFileContentDto?> ReadCompanyLogFileAsync(int? clientId, string fileName, int tailLines = 500, CancellationToken cancellationToken = default);
        Task<(byte[] Bytes, string FileName)?> ReadCompanyLogFileBytesAsync(int? clientId, string fileName, CancellationToken cancellationToken = default);
        Task<int> UpsertCustomerApiUsageAsync(CustomerApiUsageSyncRequest request, CancellationToken cancellationToken = default);
        Task<CustomerApiUsageQueryResultDto> GetCustomerApiUsageAsync(int? clientId = null, int? usageMonth = null, string? search = null, CancellationToken cancellationToken = default);
    }

    public class ApiOnlyAccessService : IApiOnlyAccessService
    {
        private static readonly SemaphoreSlim _logFileLock = new(1, 1);

        private readonly IApiOnlyAccessRepository _apiOnlyAccessRepository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ApiOnlyAccessService> _logger;
        private readonly string _logRootDir;


        public ApiOnlyAccessService(
            IApiOnlyAccessRepository apiOnlyAccessRepository,
            IHttpClientFactory httpClientFactory,
             ILogger<ApiOnlyAccessService> logger,
            IHostEnvironment hostEnvironment,
            IConfiguration configuration)
        {
            _apiOnlyAccessRepository = apiOnlyAccessRepository;
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            string? configuredDir = configuration["ErrorLogs:Directory"];
            _logRootDir = !string.IsNullOrWhiteSpace(configuredDir)
                ? Path.GetFullPath(configuredDir)
                : Path.Combine(hostEnvironment.ContentRootPath, "Logs", "CompanyApiLogs");
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
                        module_id = api?.ModuleId,
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
                                        // The remote endpoint was reached and processed the request, but failed.
                                        // Do not retry other alias routes; return the error immediately to avoid duplicate logs.
                                        return (false, err);
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

                                // If the controller returned a server error (500) or bad request (400),
                                // the endpoint exists but failed internally. Retrying alias routes (/save, /apis)
                                // would hit the exact same controller and cause duplicate rollback log entries!
                                if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
                                {
                                    return (false, lastError);
                                }
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

        public async Task<IEnumerable<ProwessRollbackLogDto>> GetRollbackLogsAsync(int? clientId = null, int limit = 100, CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.GetRollbackLogsAsync(clientId, limit, cancellationToken);
        }


        public async Task<ApiErrorLogIngestResultDto> IngestApiErrorLogsAsync(
       ApiErrorLogBatchRequest request,
       CancellationToken cancellationToken = default)
        {
            var logs = request?.Logs ?? new List<ApiErrorLogItemRequest>();
            if (logs.Count == 0)
            {
                return new ApiErrorLogIngestResultDto { InsertedCount = 0 };
            }

            var clientMap = await _apiOnlyAccessRepository.GetClientNameMapAsync(cancellationToken);
            var itemsWithPaths = new List<(ApiErrorLogItemRequest Item, string? LogFilePath)>();
            var touchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await _logFileLock.WaitAsync(cancellationToken);
            try
            {
                Directory.CreateDirectory(_logRootDir);

                foreach (var item in logs)
                {
                    int? effectiveClientId = item.ClientId.HasValue && item.ClientId.Value > 0
                        ? item.ClientId
                        : (request?.ClientId.HasValue == true && request.ClientId.Value > 0 ? request.ClientId : null);

                    string? effectiveClientName = !string.IsNullOrWhiteSpace(item.ClientName)
                        ? item.ClientName.Trim()
                        : request?.ClientName?.Trim();

                    if (effectiveClientId.HasValue && clientMap.TryGetValue(effectiveClientId.Value, out var canonicalName) && !string.IsNullOrWhiteSpace(canonicalName))
                    {
                        effectiveClientName = canonicalName;
                    }

                    DateTime occurredAt = item.OccurredAt == default ? DateTime.Now : item.OccurredAt;
                    string folderName = ResolveOrCreateCompanyFolder(effectiveClientId, effectiveClientName);
                    string fileName = $"api-errors-{occurredAt:yyyy-MM-dd}.log";
                    string fullFolder = Path.Combine(_logRootDir, folderName);
                    Directory.CreateDirectory(fullFolder);

                    string fullFilePath = Path.Combine(fullFolder, fileName);
                    string relativePath = $"{folderName}/{fileName}";

                    string formattedEntry = FormatErrorLogBlock(
                        item,
                        effectiveClientId,
                        effectiveClientName,
                        !string.IsNullOrWhiteSpace(item.SourceApiUrl) ? item.SourceApiUrl : request?.SourceApiUrl,
                        occurredAt);

                    await File.AppendAllTextAsync(fullFilePath, formattedEntry, Encoding.UTF8, cancellationToken);
                    touchedFiles.Add(relativePath);
                    itemsWithPaths.Add((item, relativePath));
                }
            }
            finally
            {
                _logFileLock.Release();
            }

            int inserted = await _apiOnlyAccessRepository.SaveApiErrorLogsAsync(
                itemsWithPaths,
                request?.ClientId,
                request?.ClientName,
                request?.SourceApiUrl,
                cancellationToken);

            return new ApiErrorLogIngestResultDto
            {
                InsertedCount = inserted,
                LogFiles = touchedFiles.OrderBy(f => f).ToList()
            };
        }

        public async Task<ApiErrorLogQueryResultDto> GetApiErrorLogsAsync(
            int? clientId = null,
            string? environment = null,
            string? statusFilter = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int page = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.GetApiErrorLogsAsync(
                clientId, environment, statusFilter, search, fromDate, toDate, page, pageSize, cancellationToken);
        }

        public async Task<IEnumerable<CompanyLogFileDto>> GetCompanyLogFilesAsync(
            int? clientId = null,
            CancellationToken cancellationToken = default)
        {
            var files = new List<CompanyLogFileDto>();
            if (!Directory.Exists(_logRootDir))
            {
                return files;
            }

            var clientMap = await _apiOnlyAccessRepository.GetClientNameMapAsync(cancellationToken);

            foreach (var dirPath in Directory.GetDirectories(_logRootDir))
            {
                string folderName = Path.GetFileName(dirPath);
                var (parsedClientId, parsedName) = ParseCompanyFolderName(folderName);

                if (clientId.HasValue && clientId.Value > 0 && parsedClientId != clientId.Value)
                {
                    continue;
                }

                string displayName = parsedClientId.HasValue && clientMap.TryGetValue(parsedClientId.Value, out var canonical)
                    ? canonical
                    : parsedName;

                foreach (var filePath in Directory.GetFiles(dirPath, "*.log", SearchOption.TopDirectoryOnly))
                {
                    var fi = new FileInfo(filePath);
                    files.Add(new CompanyLogFileDto
                    {
                        ClientId = parsedClientId,
                        ClientName = displayName,
                        FolderName = folderName,
                        FileName = fi.Name,
                        RelativePath = $"{folderName}/{fi.Name}",
                        SizeBytes = fi.Length,
                        LastModifiedUtc = fi.LastWriteTimeUtc
                    });
                }
            }

            return files
                .OrderByDescending(f => f.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(f => f.LastModifiedUtc)
                .ToList();
        }

        public async Task<CompanyLogFileContentDto?> ReadCompanyLogFileAsync(
            int? clientId,
            string fileName,
            int tailLines = 500,
            CancellationToken cancellationToken = default)
        {
            string? physicalPath = ResolveSafeLogFilePath(clientId, fileName);
            if (physicalPath == null || !File.Exists(physicalPath))
            {
                return null;
            }

            var fi = new FileInfo(physicalPath);
            string folderName = Path.GetFileName(Path.GetDirectoryName(physicalPath) ?? string.Empty);
            var (parsedClientId, parsedName) = ParseCompanyFolderName(folderName);
            var clientMap = await _apiOnlyAccessRepository.GetClientNameMapAsync(cancellationToken);
            string displayName = parsedClientId.HasValue && clientMap.TryGetValue(parsedClientId.Value, out var canonical)
                ? canonical
                : parsedName;

            string[] allLines;
            await _logFileLock.WaitAsync(cancellationToken);
            try
            {
                allLines = await File.ReadAllLinesAsync(physicalPath, Encoding.UTF8, cancellationToken);
            }
            finally
            {
                _logFileLock.Release();
            }

            int maxLines = tailLines > 0 ? Math.Min(tailLines, 5000) : allLines.Length;
            IEnumerable<string> slice = allLines.Length > maxLines
                ? allLines.Skip(allLines.Length - maxLines)
                : allLines;

            return new CompanyLogFileContentDto
            {
                ClientId = parsedClientId,
                ClientName = displayName,
                FileName = fi.Name,
                RelativePath = $"{folderName}/{fi.Name}",
                SizeBytes = fi.Length,
                TotalLines = allLines.Length,
                Content = string.Join(Environment.NewLine, slice)
            };
        }

        public async Task<(byte[] Bytes, string FileName)?> ReadCompanyLogFileBytesAsync(
            int? clientId,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            string? physicalPath = ResolveSafeLogFilePath(clientId, fileName);
            if (physicalPath == null || !File.Exists(physicalPath))
            {
                return null;
            }

            await _logFileLock.WaitAsync(cancellationToken);
            try
            {
                byte[] bytes = await File.ReadAllBytesAsync(physicalPath, cancellationToken);
                return (bytes, Path.GetFileName(physicalPath));
            }
            finally
            {
                _logFileLock.Release();
            }
        }

        public string? ResolveSafeLogFilePath(int? clientId, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(_logRootDir))
            {
                return null;
            }

            string safeFileName = Path.GetFileName(fileName.Trim());
            if (string.IsNullOrWhiteSpace(safeFileName) ||
                !safeFileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string rootFull = Path.GetFullPath(_logRootDir);
            foreach (var dirPath in Directory.GetDirectories(rootFull))
            {
                string folderName = Path.GetFileName(dirPath);
                var (parsedClientId, _) = ParseCompanyFolderName(folderName);

                if (clientId.HasValue && clientId.Value > 0 && parsedClientId != clientId.Value)
                {
                    continue;
                }
                if ((!clientId.HasValue || clientId.Value <= 0) && parsedClientId.HasValue)
                {
                    continue;
                }

                string candidate = Path.GetFullPath(Path.Combine(dirPath, safeFileName));
                if (candidate.StartsWith(dirPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private string ResolveOrCreateCompanyFolder(int? clientId, string? clientName)
        {
            if (clientId.HasValue && clientId.Value > 0)
            {
                string prefix = $"Client_{clientId.Value}_";
                if (Directory.Exists(_logRootDir))
                {
                    string? existing = Directory.GetDirectories(_logRootDir, prefix + "*")
                        .Select(Path.GetFileName)
                        .FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(existing))
                    {
                        return existing;
                    }
                }

                string safeName = SanitizeFolderPart(clientName);
                return string.IsNullOrEmpty(safeName) ? $"Client_{clientId.Value}" : $"{prefix}{safeName}";
            }

            return "Client_Unknown";
        }

        private static (int? ClientId, string ClientName) ParseCompanyFolderName(string folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName) || !folderName.StartsWith("Client_", StringComparison.OrdinalIgnoreCase))
            {
                return (null, folderName);
            }

            string rest = folderName.Substring("Client_".Length);
            if (rest.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return (null, "Unknown Company");
            }

            int idx = rest.IndexOf('_');
            if (idx < 0)
            {
                return int.TryParse(rest, out int idOnly) ? (idOnly, $"Client #{idOnly}") : (null, rest);
            }

            string idPart = rest.Substring(0, idx);
            string namePart = rest.Substring(idx + 1).Replace('_', ' ').Trim();
            int? parsedId = int.TryParse(idPart, out int cid) ? cid : null;
            return (parsedId, string.IsNullOrWhiteSpace(namePart) ? $"Client #{idPart}" : namePart);
        }

        private static string SanitizeFolderPart(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char ch in name.Trim())
            {
                if (invalid.Contains(ch) || ch == ' ' || ch == '.' || ch == '/' || ch == '\\')
                {
                    if (sb.Length > 0 && sb[^1] != '_')
                        sb.Append('_');
                }
                else
                {
                    sb.Append(ch);
                }
            }
            string result = sb.ToString().Trim('_');
            return result.Length > 60 ? result[..60].Trim('_') : result;
        }

        private static string FormatErrorLogBlock(
            ApiErrorLogItemRequest item,
            int? clientId,
            string? clientName,
            string? sourceApiUrl,
            DateTime occurredAt)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"[{occurredAt:yyyy-MM-dd HH:mm:ss.fff}] [HTTP {item.HttpStatus}] [{item.ErrorCode ?? "ERR"}] [{item.EnvironmentMode ?? "Production"}] {item.MethodName ?? "REQ"} {item.EndpointPath}");
            sb.AppendLine($"Client       : {(clientId?.ToString() ?? "-")} - {(string.IsNullOrWhiteSpace(clientName) ? "Unknown" : clientName)}");
            sb.AppendLine($"User         : {(item.UserId?.ToString() ?? "-")} ({item.UserName ?? "-"}) | IP: {item.ClientIp ?? "-"} | ApiCode: {item.ApiCode ?? "-"} | LocalLogId: {item.LocalLogId?.ToString() ?? "-"}");
            if (!string.IsNullOrWhiteSpace(sourceApiUrl))
                sb.AppendLine($"Source URL   : {sourceApiUrl}");
            if (!string.IsNullOrWhiteSpace(item.QueryParams))
                sb.AppendLine($"Query Params : {item.QueryParams}");
            if (!string.IsNullOrWhiteSpace(item.RequestBody))
                sb.AppendLine($"Request Body : {item.RequestBody}");
            if (!string.IsNullOrWhiteSpace(item.RequestHeaders))
                sb.AppendLine($"Headers      : {item.RequestHeaders}");
            sb.AppendLine($"Error        : {item.ErrorMessage ?? "-"}");
            if (!string.IsNullOrWhiteSpace(item.ExceptionDetail))
                sb.AppendLine($"Exception    : {item.ExceptionDetail}");
            sb.AppendLine();
            return sb.ToString();
        }


        public async Task<int> UpsertCustomerApiUsageAsync(CustomerApiUsageSyncRequest request, CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.UpsertCustomerApiUsageAsync(request, cancellationToken);
        }

        public async Task<CustomerApiUsageQueryResultDto> GetCustomerApiUsageAsync(
            int? clientId = null,
            int? usageMonth = null,
            string? search = null,
            CancellationToken cancellationToken = default)
        {
            return await _apiOnlyAccessRepository.GetCustomerApiUsageAsync(clientId, usageMonth, search, cancellationToken);
        }



    }
}
