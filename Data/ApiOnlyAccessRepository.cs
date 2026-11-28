using System.Data;
using Microsoft.Data.SqlClient;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Data
{
    public interface IApiOnlyAccessRepository
    {
        Task<IEnumerable<ApiGroupDto>> GetApiGroupsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ApiMethodDto>> GetApiMethodsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ProwessApiMasterDto>> GetApisAsync(CancellationToken cancellationToken = default);
        Task<ProwessApiMasterDto> CreateApiAsync(SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default);
        Task<ProwessApiMasterDto?> UpdateApiAsync(int apiMasterId, SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default);
        Task<bool> DeleteApiAsync(int apiMasterId, int userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<client_master_dto>> get_client_master(CancellationToken cancellationToken = default);
        Task<IEnumerable<api_customer_mapping_dto>> get_customer_mappings(int? clientId = null, CancellationToken cancellationToken = default);
        Task<bool> save_customer_mappings(int clientId, string? clientName, IEnumerable<api_customer_mapping_dto> mappings, int userId, Func<Task<(bool Success, string Error)>>? onBeforeCommitAsync = null, CancellationToken cancellationToken = default);
        Task<string?> get_client_hrms_link(int clientId, CancellationToken cancellationToken = default);
        Task<bool> LogRollbackAsync(int? clientId, string? clientName, string operationType, string reason, string? errorDetails = null, string? payload = null, int? userId = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<ProwessRollbackLogDto>> GetRollbackLogsAsync(int? clientId = null, int limit = 100, CancellationToken cancellationToken = default);
        Task<Dictionary<int, string>> GetClientNameMapAsync(CancellationToken cancellationToken = default);
        Task<int> SaveApiErrorLogsAsync(IEnumerable<(ApiErrorLogItemRequest Item, string? LogFilePath)> entries, int? batchClientId, string? batchClientName, string? batchSourceApiUrl, CancellationToken cancellationToken = default);
        Task<ApiErrorLogQueryResultDto> GetApiErrorLogsAsync(int? clientId = null, string? environment = null, string? statusFilter = null, string? search = null, DateTime? fromDate = null, DateTime? toDate = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
        Task<int> UpsertCustomerApiUsageAsync(CustomerApiUsageSyncRequest request, CancellationToken cancellationToken = default);
        Task<CustomerApiUsageQueryResultDto> GetCustomerApiUsageAsync(int? clientId = null, int? usageMonth = null, string? search = null, CancellationToken cancellationToken = default);
    }

    public class ApiOnlyAccessRepository : IApiOnlyAccessRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ApiOnlyAccessRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<ApiGroupDto>> GetApiGroupsAsync(CancellationToken cancellationToken = default)
        {
            var allRows = new List<ApiGroupDto>();

            const string sql = @"
                SELECT 
                    s.Submenu_id,
                    s.Submenu_name,
                    ISNULL(s.parent_id, 0) AS parent_id,
                    ISNULL(s.Menu_id, 0) AS Menu_id,
                    ISNULL(s.is_active, 'Y') AS is_active,
                    ISNULL(p.Menu_id, 0) AS Parent_Menu_Id
                FROM dbo.ERP_Submenus s
                LEFT JOIN dbo.ERP_Submenus p ON s.parent_id = p.Submenu_id
                ORDER BY s.Submenu_id ASC;";

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                int submenuIdOrd = reader.GetOrdinal("Submenu_id");
                int submenuNameOrd = reader.GetOrdinal("Submenu_name");
                int parentIdOrd = reader.GetOrdinal("parent_id");
                int menuIdOrd = reader.GetOrdinal("Menu_id");
                int activeOrd = reader.GetOrdinal("is_active");
                int parentMenuIdOrd = reader.GetOrdinal("Parent_Menu_Id");

                var parentMenuLookup = new Dictionary<int, int>();

                while (await reader.ReadAsync(cancellationToken))
                {
                    int submenuId = reader.IsDBNull(submenuIdOrd) ? 0 : Convert.ToInt32(reader.GetValue(submenuIdOrd));
                    string submenuName = reader.IsDBNull(submenuNameOrd) ? string.Empty : Convert.ToString(reader.GetValue(submenuNameOrd))?.Trim() ?? string.Empty;
                    int parentId = reader.IsDBNull(parentIdOrd) ? 0 : Convert.ToInt32(reader.GetValue(parentIdOrd));
                    int menuId = reader.IsDBNull(menuIdOrd) ? 0 : Convert.ToInt32(reader.GetValue(menuIdOrd));
                    int parentMenuId = reader.IsDBNull(parentMenuIdOrd) ? 0 : Convert.ToInt32(reader.GetValue(parentMenuIdOrd));
                    string activeStr = reader.IsDBNull(activeOrd) ? "Y" : Convert.ToString(reader.GetValue(activeOrd))?.Trim() ?? "Y";
                    char isActive = string.IsNullOrEmpty(activeStr) ? 'Y' : char.ToUpperInvariant(activeStr[0]);

                    parentMenuLookup[submenuId] = parentMenuId;

                    allRows.Add(new ApiGroupDto
                    {
                        SubmenuId = submenuId,
                        SubmenuName = submenuName,
                        GroupName = submenuName,
                        ParentId = parentId,
                        MenuId = menuId,
                        ModuleId = menuId,
                        IsActive = isActive
                    });
                }

                // Map Submenu_id -> row for resolving Menu_id when Menu_id is 0 via parent_id -> Submenu_id -> Menu_id
                var bySubmenuId = allRows
                    .GroupBy(r => r.SubmenuId)
                    .ToDictionary(g => g.Key, g => g.First());

                var activeGroups = new List<ApiGroupDto>();

                foreach (var row in allRows)
                {
                    if (row.IsActive != 'Y')
                        continue;

                    int resolvedMenuId = row.MenuId;

                    // If Menu_id is 0, consider its parent_id matched to Submenu_id's Menu_id
                    if (resolvedMenuId == 0 && parentMenuLookup.TryGetValue(row.SubmenuId, out int directParentMenuId) && directParentMenuId != 0)
                    {
                        resolvedMenuId = directParentMenuId;
                    }

                    if (resolvedMenuId == 0 && row.ParentId != 0)
                    {
                        var visited = new HashSet<int> { row.SubmenuId };
                        int currentParentId = row.ParentId;

                        while (resolvedMenuId == 0 && currentParentId != 0 && !visited.Contains(currentParentId) && bySubmenuId.TryGetValue(currentParentId, out var parentRow))
                        {
                            visited.Add(currentParentId);
                            if (parentRow.MenuId != 0)
                            {
                                resolvedMenuId = parentRow.MenuId;
                                break;
                            }
                            currentParentId = parentRow.ParentId;
                        }
                    }

                    row.MenuId = resolvedMenuId;
                    row.ModuleId = resolvedMenuId;
                    row.GroupName = row.SubmenuName;

                    activeGroups.Add(row);
                }

                return activeGroups;
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Table ERP_Submenus does not exist yet
                return new List<ApiGroupDto>();
            }
        }

        public async Task<IEnumerable<ApiMethodDto>> GetApiMethodsAsync(CancellationToken cancellationToken = default)
        {
            var methods = new List<ApiMethodDto>();

            const string sql = @"
                SELECT 
                    Method_id,
                    Method_name,
                    Description,
                    ISNULL(Sort_order, Method_id) AS Sort_order,
                    ISNULL(is_active, 'Y') AS is_active
                FROM dbo.Prowess_ApiMethod
                WHERE UPPER(LTRIM(RTRIM(ISNULL(is_active, 'Y')))) = 'Y'
                ORDER BY ISNULL(Sort_order, Method_id) ASC, Method_id ASC;";

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                int idOrd = reader.GetOrdinal("Method_id");
                int nameOrd = reader.GetOrdinal("Method_name");
                int descOrd = reader.GetOrdinal("Description");
                int sortOrd = reader.GetOrdinal("Sort_order");
                int activeOrd = reader.GetOrdinal("is_active");

                while (await reader.ReadAsync(cancellationToken))
                {
                    string activeStr = reader.IsDBNull(activeOrd) ? "Y" : Convert.ToString(reader.GetValue(activeOrd))?.Trim() ?? "Y";
                    methods.Add(new ApiMethodDto
                    {
                        MethodId = reader.IsDBNull(idOrd) ? 0 : Convert.ToInt32(reader.GetValue(idOrd)),
                        MethodName = (reader.IsDBNull(nameOrd) ? string.Empty : Convert.ToString(reader.GetValue(nameOrd))?.Trim() ?? string.Empty).ToUpperInvariant(),
                        Description = reader.IsDBNull(descOrd) ? null : Convert.ToString(reader.GetValue(descOrd))?.Trim(),
                        SortOrder = reader.IsDBNull(sortOrd) ? 0 : Convert.ToInt32(reader.GetValue(sortOrd)),
                        IsActive = string.IsNullOrEmpty(activeStr) ? 'Y' : char.ToUpperInvariant(activeStr[0])
                    });
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Table Prowess_ApiMethod does not exist yet
                return new List<ApiMethodDto>();
            }

            return methods;
        }

        public async Task<IEnumerable<ProwessApiMasterDto>> GetApisAsync(CancellationToken cancellationToken = default)
        {
            var apis = new List<ProwessApiMasterDto>();

            const string sql = @"
                SELECT 
                    a.api_master_id,
                    a.module_id,
                    m.Module_Prefix,
                    a.submenu_id,
                    a.method_id,
                    a.method_name,
                    a.endpoint_path,
                    a.api_name,
                    a.api_code,
                    a.status,
                    a.deprecated_date,
                    a.rights_action,
                    a.summary,
                    a.request_sample,
                    a.response_sample,
                    ISNULL(a.is_active, 'Y') AS is_active,
                    a.created_by,
                    a.created_date,
                    a.modified_by,
                    a.modified_date
                FROM dbo.prowess_api_master a
                LEFT JOIN dbo.Prowess_Module_Master m ON m.Module_id = a.module_id
                WHERE UPPER(LTRIM(RTRIM(ISNULL(a.is_active, 'Y')))) = 'Y'
                ORDER BY a.api_master_id DESC;";

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    apis.Add(MapApiMaster(reader));
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Table prowess_api_master does not exist yet
                return new List<ProwessApiMasterDto>();
            }

            return apis;
        }

        public async Task<ProwessApiMasterDto> CreateApiAsync(SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                DECLARE @ResolvedMethodId INT = @MethodId;
                IF (@ResolvedMethodId IS NULL OR @ResolvedMethodId <= 0)
                BEGIN
                    IF OBJECT_ID(N'dbo.Prowess_ApiMethod', N'U') IS NOT NULL
                    BEGIN
                        SELECT TOP 1 @ResolvedMethodId = Method_id
                        FROM dbo.Prowess_ApiMethod
                        WHERE UPPER(LTRIM(RTRIM(Method_name))) = UPPER(LTRIM(RTRIM(@MethodName)));
                    END
                    SET @ResolvedMethodId = ISNULL(@ResolvedMethodId, 1);
                END

                INSERT INTO dbo.prowess_api_master (
                    module_id,
                    submenu_id,
                    method_id,
                    method_name,
                    endpoint_path,
                    api_name,
                    api_code,
                    status,
                    deprecated_date,
                    rights_action,
                    summary,
                    request_sample,
                    response_sample,
                    is_active,
                    created_by,
                    created_date
                )
                VALUES (
                    @ModuleId,
                    @SubmenuId,
                    @ResolvedMethodId,
                    UPPER(LTRIM(RTRIM(@MethodName))),
                    @EndpointPath,
                    @ApiName,
                    UPPER(LTRIM(RTRIM(@ApiCode))),
                    @Status,
                    @DeprecatedDate,
                    @RightsAction,
                    @Summary,
                    @RequestSample,
                    @ResponseSample,
                    'Y',
                    @CreatedBy,
                    GETDATE()
                );

                DECLARE @NewId INT = CAST(SCOPE_IDENTITY() AS INT);

                SELECT 
                    a.api_master_id,
                    a.module_id,
                    m.Module_Prefix,
                    a.submenu_id,
                    a.method_id,
                    a.method_name,
                    a.endpoint_path,
                    a.api_name,
                    a.api_code,
                    a.status,
                    a.deprecated_date,
                    a.rights_action,
                    a.summary,
                    a.request_sample,
                    a.response_sample,
                    ISNULL(a.is_active, 'Y') AS is_active,
                    a.created_by,
                    a.created_date,
                    a.modified_by,
                    a.modified_date
                FROM dbo.prowess_api_master a
                LEFT JOIN dbo.Prowess_Module_Master m ON m.Module_id = a.module_id
                WHERE a.api_master_id = @NewId;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            AddSaveParameters(command, request);
            command.Parameters.AddWithValue("@CreatedBy", request.CreatedBy ?? (userId > 0 ? userId : 1));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapApiMaster(reader);
            }

            throw new InvalidOperationException("Failed to retrieve the newly created API record.");
        }

        public async Task<ProwessApiMasterDto?> UpdateApiAsync(int apiMasterId, SaveProwessApiRequest request, int userId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                DECLARE @ResolvedMethodId INT = @MethodId;
                IF (@ResolvedMethodId IS NULL OR @ResolvedMethodId <= 0)
                BEGIN
                    IF OBJECT_ID(N'dbo.Prowess_ApiMethod', N'U') IS NOT NULL
                    BEGIN
                        SELECT TOP 1 @ResolvedMethodId = Method_id
                        FROM dbo.Prowess_ApiMethod
                        WHERE UPPER(LTRIM(RTRIM(Method_name))) = UPPER(LTRIM(RTRIM(@MethodName)));
                    END
                    SET @ResolvedMethodId = ISNULL(@ResolvedMethodId, 1);
                END

                UPDATE dbo.prowess_api_master
                SET
                    module_id       = @ModuleId,
                    submenu_id      = @SubmenuId,
                    method_id       = @ResolvedMethodId,
                    method_name     = UPPER(LTRIM(RTRIM(@MethodName))),
                    endpoint_path   = @EndpointPath,
                    api_name        = @ApiName,
                    api_code        = UPPER(LTRIM(RTRIM(@ApiCode))),
                    status          = @Status,
                    deprecated_date = @DeprecatedDate,
                    rights_action   = @RightsAction,
                    summary         = @Summary,
                    request_sample  = @RequestSample,
                    response_sample = @ResponseSample,
                    modified_by     = @ModifiedBy,
                    modified_date   = GETDATE()
                WHERE api_master_id = @ApiMasterId;

                SELECT 
                    a.api_master_id,
                    a.module_id,
                    m.Module_Prefix,
                    a.submenu_id,
                    a.method_id,
                    a.method_name,
                    a.endpoint_path,
                    a.api_name,
                    a.api_code,
                    a.status,
                    a.deprecated_date,
                    a.rights_action,
                    a.summary,
                    a.request_sample,
                    a.response_sample,
                    ISNULL(a.is_active, 'Y') AS is_active,
                    a.created_by,
                    a.created_date,
                    a.modified_by,
                    a.modified_date
                FROM dbo.prowess_api_master a
                LEFT JOIN dbo.Prowess_Module_Master m ON m.Module_id = a.module_id
                WHERE a.api_master_id = @ApiMasterId;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@ApiMasterId", apiMasterId);
            AddSaveParameters(command, request);
            command.Parameters.AddWithValue("@ModifiedBy", request.ModifiedBy ?? (userId > 0 ? userId : 1));

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapApiMaster(reader);
            }

            return null;
        }

        public async Task<bool> DeleteApiAsync(int apiMasterId, int userId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE dbo.prowess_api_master
                SET is_active = 'N',
                    modified_by = @ModifiedBy,
                    modified_date = GETDATE()
                WHERE api_master_id = @ApiMasterId;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@ApiMasterId", apiMasterId);
            command.Parameters.AddWithValue("@ModifiedBy", userId > 0 ? userId : 1);

            int affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected > 0;
        }

        private static void AddSaveParameters(SqlCommand command, SaveProwessApiRequest request)
        {
            command.Parameters.AddWithValue("@ModuleId", request.ModuleId);
            command.Parameters.AddWithValue("@SubmenuId", request.SubmenuId);
            command.Parameters.AddWithValue("@MethodId", request.MethodId);
            command.Parameters.AddWithValue("@MethodName", (request.MethodName ?? string.Empty).Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("@EndpointPath", (request.EndpointPath ?? string.Empty).Trim());
            command.Parameters.AddWithValue("@ApiName", (request.ApiName ?? string.Empty).Trim());
            command.Parameters.AddWithValue("@ApiCode", (request.ApiCode ?? string.Empty).Trim().ToUpperInvariant());
            command.Parameters.AddWithValue("@Status", string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim());
            command.Parameters.AddWithValue("@DeprecatedDate", request.DeprecatedDate.HasValue ? (object)request.DeprecatedDate.Value.Date : DBNull.Value);
            command.Parameters.AddWithValue("@RightsAction", string.IsNullOrWhiteSpace(request.RightsAction) ? "View" : request.RightsAction.Trim());
            command.Parameters.AddWithValue("@Summary", string.IsNullOrWhiteSpace(request.Summary) ? DBNull.Value : request.Summary.Trim());
            command.Parameters.AddWithValue("@RequestSample", string.IsNullOrWhiteSpace(request.RequestSample) ? DBNull.Value : request.RequestSample.Trim());
            command.Parameters.AddWithValue("@ResponseSample", string.IsNullOrWhiteSpace(request.ResponseSample) ? DBNull.Value : request.ResponseSample.Trim());
        }

        private static ProwessApiMasterDto MapApiMaster(SqlDataReader reader)
        {
            int idOrd = reader.GetOrdinal("api_master_id");
            int modIdOrd = reader.GetOrdinal("module_id");
            int modPrefixOrd = reader.GetOrdinal("Module_Prefix");
            int subIdOrd = reader.GetOrdinal("submenu_id");
            int methIdOrd = reader.GetOrdinal("method_id");
            int methNameOrd = reader.GetOrdinal("method_name");
            int pathOrd = reader.GetOrdinal("endpoint_path");
            int nameOrd = reader.GetOrdinal("api_name");
            int codeOrd = reader.GetOrdinal("api_code");
            int statusOrd = reader.GetOrdinal("status");
            int depDateOrd = reader.GetOrdinal("deprecated_date");
            int actionOrd = reader.GetOrdinal("rights_action");
            int sumOrd = reader.GetOrdinal("summary");
            int reqOrd = reader.GetOrdinal("request_sample");
            int resOrd = reader.GetOrdinal("response_sample");
            int activeOrd = reader.GetOrdinal("is_active");
            int createdByOrd = reader.GetOrdinal("created_by");
            int createdDateOrd = reader.GetOrdinal("created_date");
            int modByOrd = reader.GetOrdinal("modified_by");
            int modDateOrd = reader.GetOrdinal("modified_date");

            string activeStr = reader.IsDBNull(activeOrd) ? "Y" : Convert.ToString(reader.GetValue(activeOrd))?.Trim() ?? "Y";

            return new ProwessApiMasterDto
            {
                ApiMasterId = reader.IsDBNull(idOrd) ? 0 : Convert.ToInt32(reader.GetValue(idOrd)),
                ModuleId = reader.IsDBNull(modIdOrd) ? 0 : Convert.ToInt32(reader.GetValue(modIdOrd)),
                ModuleCode = reader.IsDBNull(modPrefixOrd) ? null : Convert.ToString(reader.GetValue(modPrefixOrd))?.Trim(),
                SubmenuId = reader.IsDBNull(subIdOrd) ? 0 : Convert.ToInt32(reader.GetValue(subIdOrd)),
                MethodId = reader.IsDBNull(methIdOrd) ? 0 : Convert.ToInt32(reader.GetValue(methIdOrd)),
                MethodName = (reader.IsDBNull(methNameOrd) ? string.Empty : Convert.ToString(reader.GetValue(methNameOrd))?.Trim() ?? string.Empty).ToUpperInvariant(),
                EndpointPath = reader.IsDBNull(pathOrd) ? string.Empty : Convert.ToString(reader.GetValue(pathOrd))?.Trim() ?? string.Empty,
                ApiName = reader.IsDBNull(nameOrd) ? string.Empty : Convert.ToString(reader.GetValue(nameOrd))?.Trim() ?? string.Empty,
                ApiCode = reader.IsDBNull(codeOrd) ? string.Empty : Convert.ToString(reader.GetValue(codeOrd))?.Trim() ?? string.Empty,
                Status = reader.IsDBNull(statusOrd) ? "Active" : Convert.ToString(reader.GetValue(statusOrd))?.Trim() ?? "Active",
                DeprecatedDate = reader.IsDBNull(depDateOrd) ? null : Convert.ToDateTime(reader.GetValue(depDateOrd)),
                RightsAction = reader.IsDBNull(actionOrd) ? "View" : Convert.ToString(reader.GetValue(actionOrd))?.Trim() ?? "View",
                Summary = reader.IsDBNull(sumOrd) ? null : Convert.ToString(reader.GetValue(sumOrd)),
                RequestSample = reader.IsDBNull(reqOrd) ? null : Convert.ToString(reader.GetValue(reqOrd)),
                ResponseSample = reader.IsDBNull(resOrd) ? null : Convert.ToString(reader.GetValue(resOrd)),
                IsActive = string.IsNullOrEmpty(activeStr) ? 'Y' : char.ToUpperInvariant(activeStr[0]),
                CreatedBy = reader.IsDBNull(createdByOrd) ? 0 : Convert.ToInt32(reader.GetValue(createdByOrd)),
                CreatedDate = reader.IsDBNull(createdDateOrd) ? DateTime.Now : Convert.ToDateTime(reader.GetValue(createdDateOrd)),
                ModifiedBy = reader.IsDBNull(modByOrd) ? null : Convert.ToInt32(reader.GetValue(modByOrd)),
                ModifiedDate = reader.IsDBNull(modDateOrd) ? null : Convert.ToDateTime(reader.GetValue(modDateOrd))
            };
        }

        public async Task<IEnumerable<client_master_dto>> get_client_master(CancellationToken cancellationToken = default)
        {
            var list = new List<client_master_dto>();
            const string sql = @"
                SELECT 
                    Client_Id,
                    Client_Name,
                    HRMS_Admin_Link,
                    HRMS_API_Link,
                    ISNULL(HRMS_Admin_Access, 'N') AS HRMS_Admin_Access,
                    ISNULL(HRMS_API_Access, 'N') AS HRMS_API_Access,
                    ISNULL(Is_Active, 'Y') AS Is_Active
                FROM dbo.Client_Master
                ORDER BY Client_Name ASC;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using (var command = new SqlCommand(sql, connection))
            {
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                int idOrd = reader.GetOrdinal("Client_Id");
                int nameOrd = reader.GetOrdinal("Client_Name");
                int adminLinkOrd = reader.GetOrdinal("HRMS_Admin_Link");
                int apiLinkOrd = reader.GetOrdinal("HRMS_API_Link");
                int adminAccessOrd = reader.GetOrdinal("HRMS_Admin_Access");
                int apiAccessOrd = reader.GetOrdinal("HRMS_API_Access");
                int activeOrd = reader.GetOrdinal("Is_Active");

                while (await reader.ReadAsync(cancellationToken))
                {
                    string adminAccessStr = reader.IsDBNull(adminAccessOrd) ? "N" : Convert.ToString(reader.GetValue(adminAccessOrd))?.Trim() ?? "N";
                    string apiAccessStr = reader.IsDBNull(apiAccessOrd) ? "N" : Convert.ToString(reader.GetValue(apiAccessOrd))?.Trim() ?? "N";
                    string activeStr = reader.IsDBNull(activeOrd) ? "Y" : Convert.ToString(reader.GetValue(activeOrd))?.Trim() ?? "Y";

                    list.Add(new client_master_dto
                    {
                        client_id = reader.IsDBNull(idOrd) ? 0 : Convert.ToInt32(reader.GetValue(idOrd)),
                        client_name = reader.IsDBNull(nameOrd) ? string.Empty : Convert.ToString(reader.GetValue(nameOrd))?.Trim() ?? string.Empty,
                        hrms_admin_link = reader.IsDBNull(adminLinkOrd) ? null : Convert.ToString(reader.GetValue(adminLinkOrd))?.Trim(),
                        hrms_api_link = reader.IsDBNull(apiLinkOrd) ? null : Convert.ToString(reader.GetValue(apiLinkOrd))?.Trim(),
                        hrms_admin_access = string.IsNullOrEmpty(adminAccessStr) ? 'N' : char.ToUpperInvariant(adminAccessStr[0]),
                        hrms_api_access = string.IsNullOrEmpty(apiAccessStr) ? 'N' : char.ToUpperInvariant(apiAccessStr[0]),
                        is_active = string.IsNullOrEmpty(activeStr) ? 'Y' : char.ToUpperInvariant(activeStr[0])
                    });
                }
            }

            // Fetch module rights from Prowess_Client_Modules (fallback to Client_Modules)
            const string modSql = @"
                IF OBJECT_ID(N'dbo.Prowess_Client_Modules', N'U') IS NOT NULL
                    SELECT Client_Id, Module_Id FROM dbo.Prowess_Client_Modules WITH (NOLOCK);
                ELSE IF OBJECT_ID(N'dbo.Client_Modules', N'U') IS NOT NULL
                    SELECT Client_Id, Module_Id FROM dbo.Client_Modules WITH (NOLOCK);";

            var modulesByClient = new Dictionary<int, List<int>>();
            try
            {
                using var modCmd = new SqlCommand(modSql, connection);
                modCmd.CommandTimeout = 15;
                using var modReader = await modCmd.ExecuteReaderAsync(cancellationToken);
                while (await modReader.ReadAsync(cancellationToken))
                {
                    int cid = Convert.ToInt32(modReader[0]);
                    int mid = Convert.ToInt32(modReader[1]);
                    if (!modulesByClient.TryGetValue(cid, out var mList))
                    {
                        mList = new List<int>();
                        modulesByClient[cid] = mList;
                    }
                    if (!mList.Contains(mid))
                    {
                        mList.Add(mid);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[get_client_master] Module fetch warning: {ex.Message}");
            }

            foreach (var client in list)
            {
                if (modulesByClient.TryGetValue(client.client_id, out var mods))
                {
                    client.module_ids = mods;
                }
            }

            return list;
        }

        public async Task<string?> get_client_hrms_link(int clientId, CancellationToken cancellationToken = default)
        {
            const string sql = "SELECT TOP 1 HRMS_API_Link FROM dbo.Client_Master WITH (NOLOCK) WHERE Client_Id = @ClientId;";
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 15;
                command.Parameters.AddWithValue("@ClientId", clientId);
                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result == null || result == DBNull.Value ? null : Convert.ToString(result)?.Trim();
            }
            catch
            {
                return null;
            }
        }

        public async Task<IEnumerable<api_customer_mapping_dto>> get_customer_mappings(int? clientId = null, CancellationToken cancellationToken = default)
        {
            var list = new List<api_customer_mapping_dto>();

            int GetCol(SqlDataReader r, params string[] names)
            {
                for (int i = 0; i < r.FieldCount; i++)
                {
                    string fn = r.GetName(i);
                    foreach (var n in names)
                    {
                        if (string.Equals(fn, n, StringComparison.OrdinalIgnoreCase))
                            return i;
                    }
                }
                return -1;
            }

            async Task<bool> TryQueryTableAsync(SqlConnection connection, string tableName)
            {
                string whereClause = clientId.HasValue ? "WHERE Client_Id = @ClientId" : "";
                string sql = $@"
                    SELECT *
                    FROM dbo.{tableName} WITH (NOLOCK)
                    {whereClause}
                    ORDER BY Api_Customer_Mapping_Id ASC;";

                using var command = new SqlCommand(sql, connection);
                command.CommandTimeout = 30;
                if (clientId.HasValue)
                {
                    command.Parameters.AddWithValue("@ClientId", clientId.Value);
                }

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                int mapIdOrd = GetCol(reader, "Api_Customer_Mapping_Id", "ApiCustomerMappingId", "Id");
                int clientIdOrd = GetCol(reader, "Client_Id", "ClientId");
                int clientNameOrd = GetCol(reader, "Client_Name", "ClientName");
                int apiMasterIdOrd = GetCol(reader, "Api_Master_Id", "ApiMasterId");
                int apiCodeOrd = GetCol(reader, "Api_Code", "ApiCode");
                int exposureOrd = GetCol(reader, "Onward_Exposure", "OnwardExposure");
                int consumersOrd = GetCol(reader, "Consumer_Type", "Consumer_Types", "ConsumerType", "ConsumerTypes");
                int scopeOrd = GetCol(reader, "Max_Data_Scope", "MaxDataScope");
                int sensOrd = GetCol(reader, "Sensitivity");
                int credsOrd = GetCol(reader, "Credentials");
                int rateOrd = GetCol(reader, "Max_Rate_Per_Min", "MaxRatePerMin");
                int quotaOrd = GetCol(reader, "Monthly_Quota", "MonthlyQuota");
                int ipWlOrd = GetCol(reader, "Require_Ip_Whitelist", "RequireIpWhitelist");
                int idemOrd = GetCol(reader, "Require_Idempotency_Key", "RequireIdempotencyKey");
                int sbOrd = GetCol(reader, "Sandbox_Available", "SandboxAvailable");
                int tillOrd = GetCol(reader, "Valid_Till", "ValidTill");
                int activeOrd = GetCol(reader, "Is_Active", "IsActive");
                int createdByOrd = GetCol(reader, "Created_By", "CreatedBy");
                int createdDateOrd = GetCol(reader, "Created_Date", "CreatedDate");
                int modifiedByOrd = GetCol(reader, "Modified_By", "ModifiedBy");
                int modifiedDateOrd = GetCol(reader, "Modified_Date", "ModifiedDate");

                int initialCount = list.Count;

                while (await reader.ReadAsync(cancellationToken))
                {
                    string active = activeOrd >= 0 && !reader.IsDBNull(activeOrd)
                        ? Convert.ToString(reader.GetValue(activeOrd))?.Trim() ?? "Y"
                        : "Y";

                    int cid = clientIdOrd >= 0 && !reader.IsDBNull(clientIdOrd) ? Convert.ToInt32(reader.GetValue(clientIdOrd)) : 0;
                    int amid = apiMasterIdOrd >= 0 && !reader.IsDBNull(apiMasterIdOrd) ? Convert.ToInt32(reader.GetValue(apiMasterIdOrd)) : 0;
                    string code = apiCodeOrd >= 0 && !reader.IsDBNull(apiCodeOrd) ? Convert.ToString(reader.GetValue(apiCodeOrd))?.Trim() ?? string.Empty : string.Empty;

                    if (active.Equals("N", StringComparison.OrdinalIgnoreCase) || active.Equals("0", StringComparison.OrdinalIgnoreCase) || active.Equals("false", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string ipWl = ipWlOrd >= 0 && !reader.IsDBNull(ipWlOrd) ? Convert.ToString(reader.GetValue(ipWlOrd))?.Trim() ?? "N" : "N";
                    string idem = idemOrd >= 0 && !reader.IsDBNull(idemOrd) ? Convert.ToString(reader.GetValue(idemOrd))?.Trim() ?? "N" : "N";
                    string sb = sbOrd >= 0 && !reader.IsDBNull(sbOrd) ? Convert.ToString(reader.GetValue(sbOrd))?.Trim() ?? "N" : "N";

                    list.Add(new api_customer_mapping_dto
                    {
                        api_customer_mapping_id = mapIdOrd >= 0 && !reader.IsDBNull(mapIdOrd) ? Convert.ToInt32(reader.GetValue(mapIdOrd)) : 0,
                        client_id = cid,
                        client_name = clientNameOrd >= 0 && !reader.IsDBNull(clientNameOrd) ? Convert.ToString(reader.GetValue(clientNameOrd)) : null,
                        api_master_id = amid,
                        api_code = code,
                        onward_exposure = exposureOrd >= 0 && !reader.IsDBNull(exposureOrd) ? Convert.ToString(reader.GetValue(exposureOrd))?.Trim() ?? "Internal" : "Internal",
                        consumer_types = consumersOrd >= 0 && !reader.IsDBNull(consumersOrd) ? Convert.ToString(reader.GetValue(consumersOrd))?.Trim() : null,
                        max_data_scope = scopeOrd >= 0 && !reader.IsDBNull(scopeOrd) ? Convert.ToString(reader.GetValue(scopeOrd))?.Trim() ?? "Own" : "Own",
                        sensitivity = sensOrd >= 0 && !reader.IsDBNull(sensOrd) ? Convert.ToString(reader.GetValue(sensOrd))?.Trim() ?? "Normal" : "Normal",
                        credentials = credsOrd >= 0 && !reader.IsDBNull(credsOrd) ? Convert.ToString(reader.GetValue(credsOrd))?.Trim() : null,
                        max_rate_per_min = rateOrd >= 0 && !reader.IsDBNull(rateOrd) ? Convert.ToInt32(reader.GetValue(rateOrd)) : null,
                        monthly_quota = quotaOrd >= 0 && !reader.IsDBNull(quotaOrd) ? Convert.ToInt64(reader.GetValue(quotaOrd)) : null,
                        require_ip_whitelist = string.IsNullOrEmpty(ipWl) ? 'N' : char.ToUpperInvariant(ipWl[0]),
                        require_idempotency_key = string.IsNullOrEmpty(idem) ? 'N' : char.ToUpperInvariant(idem[0]),
                        sandbox_available = string.IsNullOrEmpty(sb) ? 'N' : char.ToUpperInvariant(sb[0]),
                        valid_till = tillOrd >= 0 && !reader.IsDBNull(tillOrd) ? Convert.ToDateTime(reader.GetValue(tillOrd)) : null,
                        is_active = string.IsNullOrEmpty(active) ? 'Y' : char.ToUpperInvariant(active[0]),
                        created_by = createdByOrd >= 0 && !reader.IsDBNull(createdByOrd) ? Convert.ToInt32(reader.GetValue(createdByOrd)) : 1,
                        created_date = createdDateOrd >= 0 && !reader.IsDBNull(createdDateOrd) ? Convert.ToDateTime(reader.GetValue(createdDateOrd)) : DateTime.Now,
                        modified_by = modifiedByOrd >= 0 && !reader.IsDBNull(modifiedByOrd) ? Convert.ToInt32(reader.GetValue(modifiedByOrd)) : null,
                        modified_date = modifiedDateOrd >= 0 && !reader.IsDBNull(modifiedDateOrd) ? Convert.ToDateTime(reader.GetValue(modifiedDateOrd)) : null
                    });
                }

                return (list.Count - initialCount) > 0;
            }

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

                // Priority 1: Query Prowess_Api_Customer_Mapping (official table)
                bool found = false;
                try
                {
                    found = await TryQueryTableAsync(connection, "Prowess_Api_Customer_Mapping");
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    // Table doesn't exist
                }

                // Priority 2: Fallback to ApiCustomerMapping if Prowess_Api_Customer_Mapping was empty or doesn't exist
                if (!found)
                {
                    try
                    {
                        await TryQueryTableAsync(connection, "ApiCustomerMapping");
                    }
                    catch (SqlException ex) when (ex.Number == 208)
                    {
                        // Table doesn't exist
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[get_customer_mappings] Error: {ex}");
                throw;
            }

            return list;
        }

        public async Task<bool> save_customer_mappings(
            int clientId,
            string? clientName,
            IEnumerable<api_customer_mapping_dto> mappings,
            int userId,
            Func<Task<(bool Success, string Error)>>? onBeforeCommitAsync = null,
            CancellationToken cancellationToken = default)
        {
            var mappingList = mappings?.ToList() ?? new List<api_customer_mapping_dto>();
            string payloadJson = string.Empty;
            try
            {
                payloadJson = System.Text.Json.JsonSerializer.Serialize(mappingList);
            }
            catch { }

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var transaction = connection.BeginTransaction();

            string ResolveTableName()
            {
                try
                {
                    using var checkCmd = new SqlCommand(@"
                        IF OBJECT_ID(N'dbo.Prowess_Api_Customer_Mapping', N'U') IS NOT NULL
                            SELECT 'Prowess_Api_Customer_Mapping';
                        ELSE IF OBJECT_ID(N'dbo.ApiCustomerMapping', N'U') IS NOT NULL
                            SELECT 'ApiCustomerMapping';
                        ELSE
                            SELECT 'Prowess_Api_Customer_Mapping';", connection, transaction);
                    return Convert.ToString(checkCmd.ExecuteScalar()) ?? "Prowess_Api_Customer_Mapping";
                }
                catch
                {
                    return "Prowess_Api_Customer_Mapping";
                }
            }

            try
            {
                // 1. Verify module rights from Prowess_Client_Modules (or Client_Modules)
                const string allowedModSql = @"
                    IF OBJECT_ID(N'dbo.Prowess_Client_Modules', N'U') IS NOT NULL
                        SELECT Module_Id FROM dbo.Prowess_Client_Modules WITH (NOLOCK) WHERE Client_Id = @ClientId;
                    ELSE IF OBJECT_ID(N'dbo.Client_Modules', N'U') IS NOT NULL
                        SELECT Module_Id FROM dbo.Client_Modules WITH (NOLOCK) WHERE Client_Id = @ClientId;";

                var allowedModuleIds = new HashSet<int>();
                using (var modCmd = new SqlCommand(allowedModSql, connection, transaction))
                {
                    modCmd.Parameters.AddWithValue("@ClientId", clientId);
                    using var modReader = await modCmd.ExecuteReaderAsync(cancellationToken);
                    while (await modReader.ReadAsync(cancellationToken))
                    {
                        allowedModuleIds.Add(modReader.GetInt32(0));
                    }
                }

                // 2. Fetch module_id for APIs from prowess_api_master
                const string apiModSql = "SELECT api_master_id, module_id, api_code FROM dbo.prowess_api_master WITH (NOLOCK);";
                var apiModuleMap = new Dictionary<int, (int ModuleId, string ApiCode)>();
                using (var apiCmd = new SqlCommand(apiModSql, connection, transaction))
                {
                    using var apiReader = await apiCmd.ExecuteReaderAsync(cancellationToken);
                    while (await apiReader.ReadAsync(cancellationToken))
                    {
                        int amid = apiReader.GetInt32(0);
                        int mid = apiReader.IsDBNull(1) ? 0 : apiReader.GetInt32(1);
                        string code = apiReader.IsDBNull(2) ? string.Empty : apiReader.GetString(2);
                        apiModuleMap[amid] = (mid, code);
                    }
                }

                // 3. Ensure APIs belong to allowed modules
                var unauthorizedApis = new List<string>();
                var authorizedMappings = new List<api_customer_mapping_dto>();

                foreach (var m in mappings ?? Enumerable.Empty<api_customer_mapping_dto>())
                {
                    if (m.api_master_id <= 0) continue;
                    if (apiModuleMap.TryGetValue(m.api_master_id, out var info) && info.ModuleId > 0)
                    {
                        if (!allowedModuleIds.Contains(info.ModuleId))
                        {
                            unauthorizedApis.Add(string.IsNullOrWhiteSpace(m.api_code) ? info.ApiCode : m.api_code);
                            continue;
                        }
                    }
                    authorizedMappings.Add(m);
                }

                if (unauthorizedApis.Count > 0)
                {
                    try { transaction.Rollback(); } catch { }
                    string reason = $"Customer #{clientId} does not have rights for the module(s) containing API(s): {string.Join(", ", unauthorizedApis)}. Assignment rejected.";
                    await LogRollbackAsync(clientId, clientName, "MODULE_RIGHTS_REJECTED", reason, null, payloadJson, userId, CancellationToken.None);
                    throw new InvalidOperationException(reason);
                }

                string targetTable = ResolveTableName();
                var activeApiMasterIds = authorizedMappings.Select(m => m.api_master_id).Where(id => id > 0).Distinct().ToList();

                string deactivateSql;
                if (activeApiMasterIds.Count > 0)
                {
                    string idList = string.Join(",", activeApiMasterIds);
                    deactivateSql = $@"
                        UPDATE dbo.{targetTable}
                        SET Is_Active = 'N', Modified_By = @UserId, Modified_Date = GETDATE()
                        WHERE Client_Id = @ClientId AND Api_Master_Id NOT IN ({idList});";
                }
                else
                {
                    deactivateSql = $@"
                        UPDATE dbo.{targetTable}
                        SET Is_Active = 'N', Modified_By = @UserId, Modified_Date = GETDATE()
                        WHERE Client_Id = @ClientId;";
                }

                using (var deactCmd = new SqlCommand(deactivateSql, connection, transaction))
                {
                    deactCmd.CommandTimeout = 30;
                    deactCmd.Parameters.AddWithValue("@ClientId", clientId);
                    deactCmd.Parameters.AddWithValue("@UserId", userId > 0 ? userId : 1);
                    await deactCmd.ExecuteNonQueryAsync(cancellationToken);
                }

                string upsertSql = $@"
                    MERGE dbo.{targetTable} AS target
                    USING (SELECT @ClientId AS Client_Id, @ApiMasterId AS Api_Master_Id) AS source
                    ON (target.Client_Id = source.Client_Id AND target.Api_Master_Id = source.Api_Master_Id)
                    WHEN MATCHED THEN
                        UPDATE SET 
                            target.Client_Name = @ClientName,
                            target.Api_Code = @ApiCode,
                            target.Onward_Exposure = @OnwardExposure,
                            target.Consumer_Types = @ConsumerTypes,
                            target.Max_Data_Scope = @MaxDataScope,
                            target.Sensitivity = @Sensitivity,
                            target.Credentials = @Credentials,
                            target.Max_Rate_Per_Min = @MaxRatePerMin,
                            target.Monthly_Quota = @MonthlyQuota,
                            target.Require_Ip_Whitelist = @RequireIpWhitelist,
                            target.Require_Idempotency_Key = @RequireIdempotencyKey,
                            target.Sandbox_Available = @SandboxAvailable,
                            target.Valid_Till = @ValidTill,
                            target.Is_Active = 'Y',
                            target.Modified_By = @UserId,
                            target.Modified_Date = GETDATE()
                    WHEN NOT MATCHED THEN
                        INSERT (
                            Client_Id, Client_Name, Api_Master_Id, Api_Code,
                            Onward_Exposure, Consumer_Types, Max_Data_Scope, Sensitivity, Credentials,
                            Max_Rate_Per_Min, Monthly_Quota, Require_Ip_Whitelist, Require_Idempotency_Key,
                            Sandbox_Available, Valid_Till, Is_Active, Created_By, Created_Date
                        )
                        VALUES (
                            @ClientId, @ClientName, @ApiMasterId, @ApiCode,
                            @OnwardExposure, @ConsumerTypes, @MaxDataScope, @Sensitivity, @Credentials,
                            @MaxRatePerMin, @MonthlyQuota, @RequireIpWhitelist, @RequireIdempotencyKey,
                            @SandboxAvailable, @ValidTill, 'Y', @UserId, GETDATE()
                        );";

                foreach (var m in authorizedMappings)
                {
                    if (m.api_master_id <= 0) continue;

                    using var cmd = new SqlCommand(upsertSql, connection, transaction);
                    cmd.CommandTimeout = 30;
                    cmd.Parameters.AddWithValue("@ClientId", clientId);
                    cmd.Parameters.AddWithValue("@ClientName", (object?)clientName ?? (object?)m.client_name ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@ApiMasterId", m.api_master_id);
                    cmd.Parameters.AddWithValue("@ApiCode", (object?)m.api_code ?? string.Empty);
                    cmd.Parameters.AddWithValue("@OnwardExposure", string.IsNullOrWhiteSpace(m.onward_exposure) ? "Internal" : m.onward_exposure);
                    cmd.Parameters.AddWithValue("@ConsumerTypes", string.IsNullOrWhiteSpace(m.consumer_types) ? DBNull.Value : (object)m.consumer_types);
                    cmd.Parameters.AddWithValue("@MaxDataScope", string.IsNullOrWhiteSpace(m.max_data_scope) ? "Own" : m.max_data_scope);
                    cmd.Parameters.AddWithValue("@Sensitivity", string.IsNullOrWhiteSpace(m.sensitivity) ? "Normal" : m.sensitivity);
                    cmd.Parameters.AddWithValue("@Credentials", string.IsNullOrWhiteSpace(m.credentials) ? DBNull.Value : (object)m.credentials);
                    cmd.Parameters.AddWithValue("@MaxRatePerMin", m.max_rate_per_min.HasValue ? (object)m.max_rate_per_min.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@MonthlyQuota", m.monthly_quota.HasValue ? (object)m.monthly_quota.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@RequireIpWhitelist", m.require_ip_whitelist != '\0' ? m.require_ip_whitelist : 'N');
                    cmd.Parameters.AddWithValue("@RequireIdempotencyKey", m.require_idempotency_key != '\0' ? m.require_idempotency_key : 'N');
                    cmd.Parameters.AddWithValue("@SandboxAvailable", m.sandbox_available != '\0' ? m.sandbox_available : 'N');
                    cmd.Parameters.AddWithValue("@ValidTill", m.valid_till.HasValue ? (object)m.valid_till.Value.Date : DBNull.Value);
                    cmd.Parameters.AddWithValue("@UserId", userId > 0 ? userId : 1);

                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                // Two-phase sync: push to client's HRMS database BEFORE committing local CRM database
                if (onBeforeCommitAsync != null)
                {
                    var (syncSuccess, syncError) = await onBeforeCommitAsync();
                    if (!syncSuccess)
                    {
                        try { transaction.Rollback(); } catch { }
                        string reason = $"Sync to Client HRMS database failed: {syncError}. Local CRM changes were rolled back.";
                        await LogRollbackAsync(clientId, clientName, "HRMS_SYNC_FAILED", reason, syncError, payloadJson, userId, CancellationToken.None);
                        throw new InvalidOperationException(reason);
                    }
                }

                transaction.Commit();
                return true;
            }
            catch (OperationCanceledException ex)
            {
                try { transaction.Rollback(); } catch { }
                await LogRollbackAsync(clientId, clientName, "OPERATION_CANCELED", "Operation was cancelled by client or request timed out.", ex.ToString(), payloadJson, userId, CancellationToken.None);
                return false;
            }
            catch (Exception ex)
            {
                try { transaction.Rollback(); } catch { }
                if (ex is not InvalidOperationException)
                {
                    await LogRollbackAsync(clientId, clientName, "DATABASE_ERROR", $"Database error during customer mapping save: {ex.Message}", ex.ToString(), payloadJson, userId, CancellationToken.None);
                }
                throw;
            }
        }

        public async Task<bool> LogRollbackAsync(
            int? clientId,
            string? clientName,
            string operationType,
            string reason,
            string? errorDetails = null,
            string? payload = null,
            int? userId = null,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
                IF OBJECT_ID(N'dbo.Prowess_Rollback_Log', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Prowess_Rollback_Log (
                        [Log_Id]           BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Client_Id]        INT NULL,
                        [Client_Name]      NVARCHAR(250) NULL,
                        [Operation_Type]   VARCHAR(100) NOT NULL,
                        [Reason]           NVARCHAR(MAX) NOT NULL,
                        [Error_Details]    NVARCHAR(MAX) NULL,
                        [Payload]          NVARCHAR(MAX) NULL,
                        [Created_By]       INT NULL DEFAULT 1,
                        [Created_Date]     DATETIME NOT NULL DEFAULT GETDATE()
                    );
                    CREATE NONCLUSTERED INDEX IX_Prowess_Rollback_Log_ClientId 
                    ON dbo.Prowess_Rollback_Log (Client_Id, Created_Date DESC);
                END

                INSERT INTO dbo.Prowess_Rollback_Log (
                    Client_Id, Client_Name, Operation_Type, Reason, Error_Details, Payload, Created_By, Created_Date
                )
                VALUES (
                    @ClientId, @ClientName, @OperationType, @Reason, @ErrorDetails, @Payload, @CreatedBy, GETDATE()
                );";

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var cmd = new SqlCommand(sql, connection);
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddWithValue("@ClientId", (object?)clientId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ClientName", (object?)clientName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@OperationType", string.IsNullOrWhiteSpace(operationType) ? "ROLLBACK" : operationType);
                cmd.Parameters.AddWithValue("@Reason", reason ?? "Unknown rollback reason");
                cmd.Parameters.AddWithValue("@ErrorDetails", (object?)errorDetails ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Payload", (object?)payload ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@CreatedBy", (object?)userId ?? 1);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                Console.WriteLine($"[LogRollbackAsync] 📝 Logged rollback: Client={clientId}, Type={operationType}, Reason={reason}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LogRollbackAsync] ❌ Failed to write rollback log: {ex.Message}");
                return false;
            }
        }

        public async Task<IEnumerable<ProwessRollbackLogDto>> GetRollbackLogsAsync(
            int? clientId = null,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            var list = new List<ProwessRollbackLogDto>();
            string whereClause = clientId.HasValue ? "WHERE Client_Id = @ClientId" : "";
            string sql = $@"
                SELECT TOP (@Limit)
                    Log_Id, Client_Id, Client_Name, Operation_Type, Reason, Error_Details, Payload, Created_By, Created_Date
                FROM dbo.Prowess_Rollback_Log WITH (NOLOCK)
                {whereClause}
                ORDER BY Log_Id DESC;";

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var cmd = new SqlCommand(sql, connection);
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddWithValue("@Limit", limit > 0 ? limit : 100);
                if (clientId.HasValue)
                    cmd.Parameters.AddWithValue("@ClientId", clientId.Value);

                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(new ProwessRollbackLogDto
                    {
                        LogId = reader.GetInt64(0),
                        ClientId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        ClientName = reader.IsDBNull(2) ? null : reader.GetString(2),
                        OperationType = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                        Reason = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        ErrorDetails = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Payload = reader.IsDBNull(6) ? null : reader.GetString(6),
                        CreatedBy = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                        CreatedDate = reader.GetDateTime(8)
                    });
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Table does not exist
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetRollbackLogsAsync] Error: {ex.Message}");
            }

            return list;
        }




        public async Task<Dictionary<int, string>> GetClientNameMapAsync(CancellationToken cancellationToken = default)
        {
            var map = new Dictionary<int, string>();
            const string sql = "SELECT Client_Id, Client_Name FROM dbo.Client_Master WITH (NOLOCK);";
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var cmd = new SqlCommand(sql, connection);
                cmd.CommandTimeout = 15;
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    int cid = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                    string cname = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1))?.Trim() ?? string.Empty;
                    if (cid > 0 && !string.IsNullOrWhiteSpace(cname))
                    {
                        map[cid] = cname;
                    }
                }
            }
            catch
            {
                // Ignore if Client_Master lookup fails
            }
            return map;
        }

        private static async Task EnsureApiErrorLogTableAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            const string ddl = @"
                IF OBJECT_ID(N'dbo.Prowess_Api_Error_Log', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Prowess_Api_Error_Log] (
                        [Error_Log_Id]       BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Client_Id]          INT NULL,
                        [Client_Name]        NVARCHAR(255) NULL,
                        [Source_Api_Url]     NVARCHAR(500) NULL,
                        [Local_Log_Id]       BIGINT NULL,
                        [External_User_Id]   INT NULL,
                        [External_User_Name] NVARCHAR(150) NULL,
                        [Api_Master_Id]      INT NULL,
                        [Api_Code]           NVARCHAR(100) NULL,
                        [Method_Name]        NVARCHAR(20) NULL,
                        [Endpoint_Path]      NVARCHAR(500) NOT NULL,
                        [Environment_Mode]   NVARCHAR(30) NULL,
                        [Query_Params]       NVARCHAR(MAX) NULL,
                        [Request_Body]       NVARCHAR(MAX) NULL,
                        [Request_Headers]    NVARCHAR(MAX) NULL,
                        [Http_Status]        INT NOT NULL,
                        [Error_Code]         NVARCHAR(100) NULL,
                        [Error_Message]      NVARCHAR(MAX) NULL,
                        [Exception_Detail]   NVARCHAR(MAX) NULL,
                        [Client_Ip]          NVARCHAR(100) NULL,
                        [Log_File_Path]      NVARCHAR(500) NULL,
                        [Occurred_At]        DATETIME NOT NULL,
                        [Received_Date]      DATETIME NOT NULL CONSTRAINT [DF_CRM_Prowess_Api_Error_Log_ReceivedDate] DEFAULT (GETDATE())
                    );

                    CREATE NONCLUSTERED INDEX [IX_CRM_Prowess_Api_Error_Log_Client_Date]
                        ON [dbo].[Prowess_Api_Error_Log] ([Client_Id], [Occurred_At] DESC, [Error_Log_Id] DESC);

                    CREATE NONCLUSTERED INDEX [IX_CRM_Prowess_Api_Error_Log_Status_Env]
                        ON [dbo].[Prowess_Api_Error_Log] ([Http_Status], [Environment_Mode]);
                    END
                ELSE
                BEGIN
                    IF COL_LENGTH('dbo.Prowess_Api_Error_Log', 'Source_Api_Url') IS NULL
                        ALTER TABLE [dbo].[Prowess_Api_Error_Log] ADD [Source_Api_Url] NVARCHAR(500) NULL;
                    IF COL_LENGTH('dbo.Prowess_Api_Error_Log', 'Local_Log_Id') IS NULL
                        ALTER TABLE [dbo].[Prowess_Api_Error_Log] ADD [Local_Log_Id] BIGINT NULL;
                    IF COL_LENGTH('dbo.Prowess_Api_Error_Log', 'Log_File_Path') IS NULL
                        ALTER TABLE [dbo].[Prowess_Api_Error_Log] ADD [Log_File_Path] NVARCHAR(500) NULL;
                    IF COL_LENGTH('dbo.Prowess_Api_Error_Log', 'Received_Date') IS NULL
                        ALTER TABLE [dbo].[Prowess_Api_Error_Log] ADD [Received_Date] DATETIME NOT NULL CONSTRAINT [DF_CRM_Prowess_Api_Error_Log_ReceivedDate_Alt] DEFAULT (GETDATE());
                END";

            using var cmd = new SqlCommand(ddl, connection);
            cmd.CommandTimeout = 30;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<int> SaveApiErrorLogsAsync(
            IEnumerable<(ApiErrorLogItemRequest Item, string? LogFilePath)> entries,
            int? batchClientId,
            string? batchClientName,
            string? batchSourceApiUrl,
            CancellationToken cancellationToken = default)
        {
            var entryList = entries?.ToList() ?? new List<(ApiErrorLogItemRequest Item, string? LogFilePath)>();
            if (entryList.Count == 0) return 0;

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await EnsureApiErrorLogTableAsync(connection, cancellationToken);

            var clientMap = await GetClientNameMapAsync(cancellationToken);

            const string insertSql = @"
                IF @LocalLogId IS NOT NULL AND @ClientId IS NOT NULL
                   AND EXISTS (SELECT 1 FROM dbo.Prowess_Api_Error_Log WITH (NOLOCK) WHERE Client_Id = @ClientId AND Local_Log_Id = @LocalLogId)
                BEGIN
                    SELECT 0;
                    RETURN;
                END

                INSERT INTO dbo.Prowess_Api_Error_Log (
                    Client_Id, Client_Name, Source_Api_Url, Local_Log_Id,
                    External_User_Id, External_User_Name,
                    Api_Master_Id, Api_Code, Method_Name, Endpoint_Path, Environment_Mode,
                    Query_Params, Request_Body, Request_Headers,
                    Http_Status, Error_Code, Error_Message, Exception_Detail,
                    Client_Ip, Log_File_Path, Occurred_At, Received_Date
                )
                VALUES (
                    @ClientId, @ClientName, @SourceApiUrl, @LocalLogId,
                    @ExternalUserId, @ExternalUserName,
                    @ApiMasterId, @ApiCode, @MethodName, @EndpointPath, @EnvironmentMode,
                    @QueryParams, @RequestBody, @RequestHeaders,
                    @HttpStatus, @ErrorCode, @ErrorMessage, @ExceptionDetail,
                    @ClientIp, @LogFilePath, @OccurredAt, GETDATE()
                );
                SELECT 1;";

            int inserted = 0;
            foreach (var (item, logFilePath) in entryList)
            {
                int? cid = item.ClientId.HasValue && item.ClientId.Value > 0 ? item.ClientId : batchClientId;
                string? cname = !string.IsNullOrWhiteSpace(item.ClientName) ? item.ClientName.Trim() : batchClientName?.Trim();
                if (cid.HasValue && cid.Value > 0 && clientMap.TryGetValue(cid.Value, out var canonicalName) && !string.IsNullOrWhiteSpace(canonicalName))
                {
                    cname = canonicalName;
                }
                string? sourceUrl = !string.IsNullOrWhiteSpace(item.SourceApiUrl) ? item.SourceApiUrl.Trim() : batchSourceApiUrl?.Trim();

                using var cmd = new SqlCommand(insertSql, connection);
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddWithValue("@ClientId", cid.HasValue && cid.Value > 0 ? (object)cid.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@ClientName", string.IsNullOrWhiteSpace(cname) ? DBNull.Value : (object)cname);
                cmd.Parameters.AddWithValue("@SourceApiUrl", string.IsNullOrWhiteSpace(sourceUrl) ? DBNull.Value : (object)sourceUrl);
                cmd.Parameters.AddWithValue("@LocalLogId", item.LocalLogId.HasValue && item.LocalLogId.Value > 0 ? (object)item.LocalLogId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@ExternalUserId", item.UserId.HasValue && item.UserId.Value > 0 ? (object)item.UserId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@ExternalUserName", string.IsNullOrWhiteSpace(item.UserName) ? DBNull.Value : (object)item.UserName.Trim());
                cmd.Parameters.AddWithValue("@ApiMasterId", item.ApiMasterId.HasValue && item.ApiMasterId.Value > 0 ? (object)item.ApiMasterId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@ApiCode", string.IsNullOrWhiteSpace(item.ApiCode) ? DBNull.Value : (object)item.ApiCode.Trim());
                cmd.Parameters.AddWithValue("@MethodName", string.IsNullOrWhiteSpace(item.MethodName) ? DBNull.Value : (object)item.MethodName.Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@EndpointPath", string.IsNullOrWhiteSpace(item.EndpointPath) ? "/" : item.EndpointPath.Trim());
                cmd.Parameters.AddWithValue("@EnvironmentMode", string.IsNullOrWhiteSpace(item.EnvironmentMode) ? DBNull.Value : (object)item.EnvironmentMode.Trim());
                cmd.Parameters.AddWithValue("@QueryParams", string.IsNullOrWhiteSpace(item.QueryParams) ? DBNull.Value : (object)item.QueryParams);
                cmd.Parameters.AddWithValue("@RequestBody", string.IsNullOrWhiteSpace(item.RequestBody) ? DBNull.Value : (object)item.RequestBody);
                cmd.Parameters.AddWithValue("@RequestHeaders", string.IsNullOrWhiteSpace(item.RequestHeaders) ? DBNull.Value : (object)item.RequestHeaders);
                cmd.Parameters.AddWithValue("@HttpStatus", item.HttpStatus);
                cmd.Parameters.AddWithValue("@ErrorCode", string.IsNullOrWhiteSpace(item.ErrorCode) ? DBNull.Value : (object)item.ErrorCode.Trim());
                cmd.Parameters.AddWithValue("@ErrorMessage", string.IsNullOrWhiteSpace(item.ErrorMessage) ? DBNull.Value : (object)item.ErrorMessage);
                cmd.Parameters.AddWithValue("@ExceptionDetail", string.IsNullOrWhiteSpace(item.ExceptionDetail) ? DBNull.Value : (object)item.ExceptionDetail);
                cmd.Parameters.AddWithValue("@ClientIp", string.IsNullOrWhiteSpace(item.ClientIp) ? DBNull.Value : (object)item.ClientIp.Trim());
                cmd.Parameters.AddWithValue("@LogFilePath", string.IsNullOrWhiteSpace(logFilePath) ? DBNull.Value : (object)logFilePath);
                cmd.Parameters.AddWithValue("@OccurredAt", item.OccurredAt == default ? DateTime.Now : item.OccurredAt);

                var res = await cmd.ExecuteScalarAsync(cancellationToken);
                if (res != null && res != DBNull.Value && Convert.ToInt32(res) == 1)
                {
                    inserted++;
                }
            }

            return inserted;
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
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? 50 : Math.Min(pageSize, 200);

            var result = new ApiErrorLogQueryResultDto
            {
                Page = page,
                PageSize = pageSize
            };

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                await EnsureApiErrorLogTableAsync(connection, cancellationToken);

                var filters = new List<string>();
                void AddCommonParams(SqlCommand cmd)
                {
                    if (clientId.HasValue && clientId.Value > 0)
                        cmd.Parameters.AddWithValue("@ClientId", clientId.Value);
                    if (!string.IsNullOrWhiteSpace(environment) && !environment.Equals("All", StringComparison.OrdinalIgnoreCase))
                        cmd.Parameters.AddWithValue("@Environment", environment.Trim());
                    if (int.TryParse(statusFilter, out int exactStatus))
                        cmd.Parameters.AddWithValue("@ExactStatus", exactStatus);
                    if (fromDate.HasValue)
                        cmd.Parameters.AddWithValue("@FromDate", fromDate.Value.Date);
                    if (toDate.HasValue)
                        cmd.Parameters.AddWithValue("@ToDate", toDate.Value.Date.AddDays(1));
                    if (!string.IsNullOrWhiteSpace(search))
                        cmd.Parameters.AddWithValue("@Search", "%" + search.Trim() + "%");
                }

                if (clientId.HasValue && clientId.Value > 0)
                    filters.Add("l.Client_Id = @ClientId");
                if (!string.IsNullOrWhiteSpace(environment) && !environment.Equals("All", StringComparison.OrdinalIgnoreCase))
                    filters.Add("UPPER(LTRIM(RTRIM(ISNULL(l.Environment_Mode, '')))) = UPPER(@Environment)");
                if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (statusFilter.Equals("4xx", StringComparison.OrdinalIgnoreCase))
                        filters.Add("l.Http_Status >= 400 AND l.Http_Status < 500");
                    else if (statusFilter.Equals("5xx", StringComparison.OrdinalIgnoreCase))
                        filters.Add("l.Http_Status >= 500");
                    else if (int.TryParse(statusFilter, out _))
                        filters.Add("l.Http_Status = @ExactStatus");
                }
                if (fromDate.HasValue)
                    filters.Add("l.Occurred_At >= @FromDate");
                if (toDate.HasValue)
                    filters.Add("l.Occurred_At < @ToDate");
                if (!string.IsNullOrWhiteSpace(search))
                {
                    filters.Add(@"(
                        ISNULL(l.Endpoint_Path, '') LIKE @Search OR
                        ISNULL(l.Error_Code, '') LIKE @Search OR
                        ISNULL(l.Error_Message, '') LIKE @Search OR
                        ISNULL(l.External_User_Name, '') LIKE @Search OR
                        ISNULL(l.Api_Code, '') LIKE @Search OR
                        ISNULL(l.Query_Params, '') LIKE @Search OR
                        ISNULL(l.Request_Body, '') LIKE @Search OR
                        ISNULL(cm.Client_Name, l.Client_Name) LIKE @Search
                    )");
                }

                string whereSql = filters.Count > 0 ? "WHERE " + string.Join(" AND ", filters) : "";

                // 1. Summary + TotalCount for current filter
                string summarySql = $@"
                    SELECT
                        COUNT(1) AS TotalCount,
                        SUM(CASE WHEN l.Http_Status >= 400 AND l.Http_Status < 500 THEN 1 ELSE 0 END) AS ClientErrors4xx,
                        SUM(CASE WHEN l.Http_Status >= 500 THEN 1 ELSE 0 END) AS ServerErrors5xx,
                        SUM(CASE WHEN CAST(l.Occurred_At AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS TodayErrors,
                        COUNT(DISTINCT l.Client_Id) AS AffectedCompanies
                    FROM dbo.Prowess_Api_Error_Log l WITH (NOLOCK)
                    LEFT JOIN dbo.Client_Master cm WITH (NOLOCK) ON cm.Client_Id = l.Client_Id
                    {whereSql};";

                using (var sumCmd = new SqlCommand(summarySql, connection))
                {
                    sumCmd.CommandTimeout = 20;
                    AddCommonParams(sumCmd);
                    using var sumRdr = await sumCmd.ExecuteReaderAsync(cancellationToken);
                    if (await sumRdr.ReadAsync(cancellationToken))
                    {
                        result.TotalCount = sumRdr.IsDBNull(0) ? 0 : Convert.ToInt32(sumRdr.GetValue(0));
                        result.Summary = new ApiErrorLogSummaryDto
                        {
                            TotalErrors = result.TotalCount,
                            ClientErrors4xx = sumRdr.IsDBNull(1) ? 0 : Convert.ToInt32(sumRdr.GetValue(1)),
                            ServerErrors5xx = sumRdr.IsDBNull(2) ? 0 : Convert.ToInt32(sumRdr.GetValue(2)),
                            TodayErrors = sumRdr.IsDBNull(3) ? 0 : Convert.ToInt32(sumRdr.GetValue(3)),
                            AffectedCompanies = sumRdr.IsDBNull(4) ? 0 : Convert.ToInt32(sumRdr.GetValue(4))
                        };
                    }
                }

                // 2. Company-wise error counts (overall so sidebar counts stay populated)
                const string companyCountSql = @"
                    SELECT
                        l.Client_Id,
                        COALESCE(NULLIF(LTRIM(RTRIM(cm.Client_Name)), ''), NULLIF(LTRIM(RTRIM(MAX(l.Client_Name))), ''), 'Unknown Company') AS Client_Name,
                        COUNT(1) AS ErrorCount,
                        MAX(l.Occurred_At) AS LastErrorAt
                    FROM dbo.Prowess_Api_Error_Log l WITH (NOLOCK)
                    LEFT JOIN dbo.Client_Master cm WITH (NOLOCK) ON cm.Client_Id = l.Client_Id
                    GROUP BY l.Client_Id, cm.Client_Name
                    ORDER BY MAX(l.Occurred_At) DESC;";

                using (var ccCmd = new SqlCommand(companyCountSql, connection))
                {
                    ccCmd.CommandTimeout = 20;
                    using var ccRdr = await ccCmd.ExecuteReaderAsync(cancellationToken);
                    while (await ccRdr.ReadAsync(cancellationToken))
                    {
                        result.CompanyCounts.Add(new CompanyErrorCountDto
                        {
                            ClientId = ccRdr.IsDBNull(0) ? null : Convert.ToInt32(ccRdr.GetValue(0)),
                            ClientName = ccRdr.IsDBNull(1) ? "Unknown Company" : Convert.ToString(ccRdr.GetValue(1)) ?? "Unknown Company",
                            ErrorCount = ccRdr.IsDBNull(2) ? 0 : Convert.ToInt32(ccRdr.GetValue(2)),
                            LastErrorAt = ccRdr.IsDBNull(3) ? null : Convert.ToDateTime(ccRdr.GetValue(3))
                        });
                    }
                }

                // 3. Paginated rows
                int offset = (page - 1) * pageSize;
                string rowsSql = $@"
                    SELECT
                        l.Error_Log_Id,
                        l.Client_Id,
                        COALESCE(NULLIF(LTRIM(RTRIM(cm.Client_Name)), ''), l.Client_Name) AS Client_Name,
                        l.Source_Api_Url,
                        l.Local_Log_Id,
                        l.External_User_Id,
                        l.External_User_Name,
                        l.Api_Master_Id,
                        l.Api_Code,
                        l.Method_Name,
                        l.Endpoint_Path,
                        l.Environment_Mode,
                        l.Query_Params,
                        l.Request_Body,
                        l.Request_Headers,
                        l.Http_Status,
                        l.Error_Code,
                        l.Error_Message,
                        l.Exception_Detail,
                        l.Client_Ip,
                        l.Log_File_Path,
                        l.Occurred_At,
                        l.Received_Date
                    FROM dbo.Prowess_Api_Error_Log l WITH (NOLOCK)
                    LEFT JOIN dbo.Client_Master cm WITH (NOLOCK) ON cm.Client_Id = l.Client_Id
                    {whereSql}
                    ORDER BY l.Occurred_At DESC, l.Error_Log_Id DESC
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

                using (var rowsCmd = new SqlCommand(rowsSql, connection))
                {
                    rowsCmd.CommandTimeout = 20;
                    AddCommonParams(rowsCmd);
                    rowsCmd.Parameters.AddWithValue("@Offset", offset);
                    rowsCmd.Parameters.AddWithValue("@PageSize", pageSize);

                    using var rdr = await rowsCmd.ExecuteReaderAsync(cancellationToken);
                    while (await rdr.ReadAsync(cancellationToken))
                    {
                        result.Items.Add(new ApiErrorLogDto
                        {
                            ErrorLogId = rdr.GetInt64(0),
                            ClientId = rdr.IsDBNull(1) ? null : Convert.ToInt32(rdr.GetValue(1)),
                            ClientName = rdr.IsDBNull(2) ? null : Convert.ToString(rdr.GetValue(2)),
                            SourceApiUrl = rdr.IsDBNull(3) ? null : Convert.ToString(rdr.GetValue(3)),
                            LocalLogId = rdr.IsDBNull(4) ? null : Convert.ToInt64(rdr.GetValue(4)),
                            UserId = rdr.IsDBNull(5) ? null : Convert.ToInt32(rdr.GetValue(5)),
                            UserName = rdr.IsDBNull(6) ? null : Convert.ToString(rdr.GetValue(6)),
                            ApiMasterId = rdr.IsDBNull(7) ? null : Convert.ToInt32(rdr.GetValue(7)),
                            ApiCode = rdr.IsDBNull(8) ? null : Convert.ToString(rdr.GetValue(8)),
                            MethodName = rdr.IsDBNull(9) ? null : Convert.ToString(rdr.GetValue(9)),
                            EndpointPath = rdr.IsDBNull(10) ? string.Empty : Convert.ToString(rdr.GetValue(10)) ?? string.Empty,
                            EnvironmentMode = rdr.IsDBNull(11) ? null : Convert.ToString(rdr.GetValue(11)),
                            QueryParams = rdr.IsDBNull(12) ? null : Convert.ToString(rdr.GetValue(12)),
                            RequestBody = rdr.IsDBNull(13) ? null : Convert.ToString(rdr.GetValue(13)),
                            RequestHeaders = rdr.IsDBNull(14) ? null : Convert.ToString(rdr.GetValue(14)),
                            HttpStatus = rdr.GetInt32(15),
                            ErrorCode = rdr.IsDBNull(16) ? null : Convert.ToString(rdr.GetValue(16)),
                            ErrorMessage = rdr.IsDBNull(17) ? null : Convert.ToString(rdr.GetValue(17)),
                            ExceptionDetail = rdr.IsDBNull(18) ? null : Convert.ToString(rdr.GetValue(18)),
                            ClientIp = rdr.IsDBNull(19) ? null : Convert.ToString(rdr.GetValue(19)),
                            LogFilePath = rdr.IsDBNull(20) ? null : Convert.ToString(rdr.GetValue(20)),
                            OccurredAt = rdr.GetDateTime(21),
                            ReceivedDate = rdr.GetDateTime(22)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GetApiErrorLogsAsync] Error: {ex.Message}");
                throw;
            }

            return result;
        }


        private static async Task EnsureCustomerApiUsageTableAsync(SqlConnection connection, CancellationToken cancellationToken)
        {
            const string ddl = @"
                IF OBJECT_ID(N'dbo.Prowess_Customer_Api_Usage_Monthly', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[Prowess_Customer_Api_Usage_Monthly] (
                        [Usage_Id]         BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Client_Id]        INT NOT NULL,
                        [Client_Name]      NVARCHAR(255) NULL,
                        [Api_Master_Id]    INT NOT NULL,
                        [Api_Code]         NVARCHAR(100) NULL,
                        [Method_Name]      NVARCHAR(20) NULL,
                        [Endpoint_Path]    NVARCHAR(500) NULL,
                        [Api_Name]         NVARCHAR(255) NULL,
                        [Usage_Month]      INT NOT NULL,
                        [Hit_Count]        BIGINT NOT NULL CONSTRAINT [DF_CRM_CustUsage_HitCount] DEFAULT (0),
                        [Monthly_Quota]    BIGINT NULL,
                        [Last_Hit_Date]    DATETIME NULL,
                        [Source_Api_Url]   NVARCHAR(500) NULL,
                        [Last_Synced_Date] DATETIME NOT NULL CONSTRAINT [DF_CRM_CustUsage_LastSynced] DEFAULT (GETDATE()),
                        CONSTRAINT [UQ_CRM_Prowess_Customer_Api_Usage_Monthly] UNIQUE ([Client_Id], [Api_Master_Id], [Usage_Month])
                    );

                    CREATE NONCLUSTERED INDEX [IX_CRM_CustUsage_Month_Client]
                        ON [dbo].[Prowess_Customer_Api_Usage_Monthly] ([Usage_Month], [Client_Id]);
                END";

            using var cmd = new SqlCommand(ddl, connection);
            cmd.CommandTimeout = 30;
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<int> UpsertCustomerApiUsageAsync(
            CustomerApiUsageSyncRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.ClientId <= 0 || request.Items == null || request.Items.Count == 0)
            {
                return 0;
            }

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await EnsureCustomerApiUsageTableAsync(connection, cancellationToken);

            var clientMap = await GetClientNameMapAsync(cancellationToken);
            string? canonicalClientName = clientMap.TryGetValue(request.ClientId, out var cName) && !string.IsNullOrWhiteSpace(cName)
                ? cName
                : request.ClientName?.Trim();

            const string upsertSql = @"
                MERGE dbo.Prowess_Customer_Api_Usage_Monthly AS target
                USING (SELECT @ClientId AS Client_Id, @ApiMasterId AS Api_Master_Id, @UsageMonth AS Usage_Month) AS src
                ON (target.Client_Id = src.Client_Id AND target.Api_Master_Id = src.Api_Master_Id AND target.Usage_Month = src.Usage_Month)
                WHEN MATCHED THEN
                    UPDATE SET
                        target.Client_Name      = COALESCE(@ClientName, target.Client_Name),
                        target.Api_Code         = COALESCE(@ApiCode, target.Api_Code),
                        target.Method_Name      = COALESCE(@MethodName, target.Method_Name),
                        target.Endpoint_Path    = COALESCE(@EndpointPath, target.Endpoint_Path),
                        target.Api_Name         = COALESCE(@ApiName, target.Api_Name),
                        target.Hit_Count        = @HitCount,
                        target.Monthly_Quota    = COALESCE(@MonthlyQuota, target.Monthly_Quota),
                        target.Last_Hit_Date    = COALESCE(@LastHitDate, target.Last_Hit_Date),
                        target.Source_Api_Url   = COALESCE(@SourceApiUrl, target.Source_Api_Url),
                        target.Last_Synced_Date = GETDATE()
                WHEN NOT MATCHED THEN
                    INSERT (
                        Client_Id, Client_Name, Api_Master_Id, Api_Code, Method_Name, Endpoint_Path, Api_Name,
                        Usage_Month, Hit_Count, Monthly_Quota, Last_Hit_Date, Source_Api_Url, Last_Synced_Date
                    )
                    VALUES (
                        @ClientId, @ClientName, @ApiMasterId, @ApiCode, @MethodName, @EndpointPath, @ApiName,
                        @UsageMonth, @HitCount, @MonthlyQuota, @LastHitDate, @SourceApiUrl, GETDATE()
                    );";

            int saved = 0;
            foreach (var item in request.Items)
            {
                if (item.ApiMasterId <= 0 || item.UsageMonth <= 0) continue;

                using var cmd = new SqlCommand(upsertSql, connection);
                cmd.CommandTimeout = 15;
                cmd.Parameters.AddWithValue("@ClientId", request.ClientId);
                cmd.Parameters.AddWithValue("@ClientName", string.IsNullOrWhiteSpace(canonicalClientName) ? DBNull.Value : (object)canonicalClientName);
                cmd.Parameters.AddWithValue("@ApiMasterId", item.ApiMasterId);
                cmd.Parameters.AddWithValue("@ApiCode", string.IsNullOrWhiteSpace(item.ApiCode) ? DBNull.Value : (object)item.ApiCode.Trim());
                cmd.Parameters.AddWithValue("@MethodName", string.IsNullOrWhiteSpace(item.MethodName) ? DBNull.Value : (object)item.MethodName.Trim().ToUpperInvariant());
                cmd.Parameters.AddWithValue("@EndpointPath", string.IsNullOrWhiteSpace(item.EndpointPath) ? DBNull.Value : (object)item.EndpointPath.Trim());
                cmd.Parameters.AddWithValue("@ApiName", string.IsNullOrWhiteSpace(item.ApiName) ? DBNull.Value : (object)item.ApiName.Trim());
                cmd.Parameters.AddWithValue("@UsageMonth", item.UsageMonth);
                cmd.Parameters.AddWithValue("@HitCount", item.HitCount < 0 ? 0 : item.HitCount);
                cmd.Parameters.AddWithValue("@MonthlyQuota", item.MonthlyQuota.HasValue ? (object)item.MonthlyQuota.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@LastHitDate", item.LastHitDate.HasValue ? (object)item.LastHitDate.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@SourceApiUrl", string.IsNullOrWhiteSpace(request.SourceApiUrl) ? DBNull.Value : (object)request.SourceApiUrl.Trim());

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                saved++;
            }

            return saved;
        }

        public async Task<CustomerApiUsageQueryResultDto> GetCustomerApiUsageAsync(
            int? clientId = null,
            int? usageMonth = null,
            string? search = null,
            CancellationToken cancellationToken = default)
        {
            int currentMonth = int.Parse(DateTime.Now.ToString("yyyyMM"));
            int effectiveMonth = usageMonth ?? currentMonth; // 0 = All Months

            var result = new CustomerApiUsageQueryResultDto
            {
                UsageMonth = effectiveMonth
            };

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            await EnsureCustomerApiUsageTableAsync(connection, cancellationToken);

            // 1. Available months
            var months = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a))) { currentMonth };
            const string monthsSql = "SELECT DISTINCT Usage_Month FROM dbo.Prowess_Customer_Api_Usage_Monthly WITH (NOLOCK);";
            using (var mCmd = new SqlCommand(monthsSql, connection))
            using (var mRdr = await mCmd.ExecuteReaderAsync(cancellationToken))
            {
                while (await mRdr.ReadAsync(cancellationToken))
                {
                    if (!mRdr.IsDBNull(0)) months.Add(mRdr.GetInt32(0));
                }
            }
            result.AvailableMonths = months.ToList();

            // Resolve mapping table name in CRM DB
            string mappingTable = "Prowess_Api_Customer_Mapping";
            using (var tCmd = new SqlCommand(@"
                IF OBJECT_ID(N'dbo.Prowess_Api_Customer_Mapping', N'U') IS NOT NULL SELECT 'Prowess_Api_Customer_Mapping';
                ELSE IF OBJECT_ID(N'dbo.ApiCustomerMapping', N'U') IS NOT NULL SELECT 'ApiCustomerMapping';
                ELSE SELECT '';", connection))
            {
                mappingTable = Convert.ToString(await tCmd.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
            }

            // 2. Load customer-wise usage rows (combining active licensed mappings + recorded usage)
            var allRows = new List<CustomerApiUsageDto>();

            if (effectiveMonth > 0 && !string.IsNullOrEmpty(mappingTable))
            {
                string sql = $@"
                    SELECT
                        COALESCE(u.Client_Id, m.Client_Id) AS Client_Id,
                        COALESCE(NULLIF(LTRIM(RTRIM(cm.Client_Name)), ''), NULLIF(LTRIM(RTRIM(u.Client_Name)), ''), NULLIF(LTRIM(RTRIM(m.Client_Name)), ''), 'Client #' + CAST(COALESCE(u.Client_Id, m.Client_Id) AS VARCHAR(20))) AS Client_Name,
                        COALESCE(u.Api_Master_Id, m.Api_Master_Id) AS Api_Master_Id,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.api_code)), ''), NULLIF(LTRIM(RTRIM(m.Api_Code)), ''), u.Api_Code, '') AS Api_Code,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.method_name)), ''), u.Method_Name, '') AS Method_Name,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.endpoint_path)), ''), u.Endpoint_Path, '') AS Endpoint_Path,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.api_name)), ''), u.Api_Name, '') AS Api_Name,
                        @TargetMonth AS Usage_Month,
                        ISNULL(u.Hit_Count, 0) AS Hit_Count,
                        COALESCE(m.Monthly_Quota, u.Monthly_Quota) AS Monthly_Quota,
                        u.Last_Hit_Date,
                        u.Last_Synced_Date,
                        u.Source_Api_Url
                    FROM (
                        SELECT * FROM dbo.{mappingTable} WITH (NOLOCK)
                        WHERE UPPER(LTRIM(RTRIM(ISNULL(Is_Active, 'Y')))) = 'Y'
                    ) m
                    FULL OUTER JOIN (
                        SELECT * FROM dbo.Prowess_Customer_Api_Usage_Monthly WITH (NOLOCK)
                        WHERE Usage_Month = @TargetMonth
                    ) u
                        ON u.Client_Id = m.Client_Id AND u.Api_Master_Id = m.Api_Master_Id
                    LEFT JOIN dbo.Client_Master cm WITH (NOLOCK)
                        ON cm.Client_Id = COALESCE(u.Client_Id, m.Client_Id)
                    LEFT JOIN dbo.prowess_api_master am WITH (NOLOCK)
                        ON am.api_master_id = COALESCE(u.Api_Master_Id, m.Api_Master_Id)
                    WHERE COALESCE(u.Client_Id, m.Client_Id) IS NOT NULL
                    ORDER BY ISNULL(u.Hit_Count, 0) DESC, Client_Name ASC, Api_Code ASC;";

                using var cmd = new SqlCommand(sql, connection);
                cmd.CommandTimeout = 25;
                cmd.Parameters.AddWithValue("@TargetMonth", effectiveMonth);
                using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    allRows.Add(MapCustomerUsageRow(rdr));
                }
            }
            else
            {
                string monthWhere = effectiveMonth > 0 ? "WHERE u.Usage_Month = @TargetMonth" : "";
                string joinMap = !string.IsNullOrEmpty(mappingTable)
                    ? $"LEFT JOIN dbo.{mappingTable} m WITH (NOLOCK) ON m.Client_Id = u.Client_Id AND m.Api_Master_Id = u.Api_Master_Id AND UPPER(LTRIM(RTRIM(ISNULL(m.Is_Active, 'Y')))) = 'Y'"
                    : "";
                string quotaExpr = !string.IsNullOrEmpty(mappingTable) ? "COALESCE(m.Monthly_Quota, u.Monthly_Quota)" : "u.Monthly_Quota";

                string sql = $@"
                    SELECT
                        u.Client_Id,
                        COALESCE(NULLIF(LTRIM(RTRIM(cm.Client_Name)), ''), NULLIF(LTRIM(RTRIM(u.Client_Name)), ''), 'Client #' + CAST(u.Client_Id AS VARCHAR(20))) AS Client_Name,
                        u.Api_Master_Id,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.api_code)), ''), u.Api_Code, '') AS Api_Code,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.method_name)), ''), u.Method_Name, '') AS Method_Name,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.endpoint_path)), ''), u.Endpoint_Path, '') AS Endpoint_Path,
                        COALESCE(NULLIF(LTRIM(RTRIM(am.api_name)), ''), u.Api_Name, '') AS Api_Name,
                        u.Usage_Month,
                        u.Hit_Count,
                        {quotaExpr} AS Monthly_Quota,
                        u.Last_Hit_Date,
                        u.Last_Synced_Date,
                        u.Source_Api_Url
                    FROM dbo.Prowess_Customer_Api_Usage_Monthly u WITH (NOLOCK)
                    {joinMap}
                    LEFT JOIN dbo.Client_Master cm WITH (NOLOCK) ON cm.Client_Id = u.Client_Id
                    LEFT JOIN dbo.prowess_api_master am WITH (NOLOCK) ON am.api_master_id = u.Api_Master_Id
                    {monthWhere}
                    ORDER BY u.Usage_Month DESC, u.Hit_Count DESC, Client_Name ASC;";

                using var cmd = new SqlCommand(sql, connection);
                cmd.CommandTimeout = 25;
                if (effectiveMonth > 0) cmd.Parameters.AddWithValue("@TargetMonth", effectiveMonth);
                using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await rdr.ReadAsync(cancellationToken))
                {
                    allRows.Add(MapCustomerUsageRow(rdr));
                }
            }

            // Build CompanySummaries across all clients (before clientId filter so sidebar stays complete)
            result.CompanySummaries = allRows
                .GroupBy(r => new { r.ClientId, r.ClientName })
                .Select(g => new CompanyUsageSummaryDto
                {
                    ClientId = g.Key.ClientId,
                    ClientName = g.Key.ClientName,
                    TotalHits = g.Sum(x => x.HitCount),
                    TotalQuota = g.Sum(x => x.MonthlyQuota ?? 0),
                    ApiCount = g.Select(x => x.ApiMasterId).Distinct().Count(),
                    LastHitDate = g.Max(x => x.LastHitDate)
                })
                .OrderByDescending(c => c.TotalHits)
                .ThenBy(c => c.ClientName)
                .ToList();

            // Apply clientId & search filters for Items + Summary
            IEnumerable<CustomerApiUsageDto> filtered = allRows;
            if (clientId.HasValue && clientId.Value > 0)
            {
                filtered = filtered.Where(r => r.ClientId == clientId.Value);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                string q = search.Trim();
                filtered = filtered.Where(r =>
                    r.ClientName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.ApiCode.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.ApiName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.EndpointPath.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    r.MethodName.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            var filteredList = filtered.ToList();
            result.Items = filteredList;
            result.Summary = new CustomerApiUsageSummaryDto
            {
                TotalHits = filteredList.Sum(x => x.HitCount),
                TotalQuota = filteredList.Sum(x => x.MonthlyQuota ?? 0),
                ActiveApisUsed = filteredList.Count(x => x.HitCount > 0),
                TotalLicensedApis = filteredList.Count,
                CustomersWithUsage = filteredList.Where(x => x.HitCount > 0).Select(x => x.ClientId).Distinct().Count(),
                NearOrOverLimitCount = filteredList.Count(x => x.UsagePercent.HasValue && x.UsagePercent.Value >= 80.0)
            };

            return result;
        }

        private static CustomerApiUsageDto MapCustomerUsageRow(SqlDataReader rdr)
        {
            long hitCount = rdr.IsDBNull(8) ? 0 : Convert.ToInt64(rdr.GetValue(8));
            long? quota = rdr.IsDBNull(9) ? null : Convert.ToInt64(rdr.GetValue(9));
            long? remaining = quota.HasValue ? Math.Max(0, quota.Value - hitCount) : null;
            double? pct = quota.HasValue && quota.Value > 0
                ? Math.Round((double)hitCount * 100.0 / quota.Value, 1)
                : null;

            return new CustomerApiUsageDto
            {
                ClientId = rdr.GetInt32(0),
                ClientName = rdr.IsDBNull(1) ? string.Empty : Convert.ToString(rdr.GetValue(1)) ?? string.Empty,
                ApiMasterId = rdr.GetInt32(2),
                ApiCode = rdr.IsDBNull(3) ? string.Empty : Convert.ToString(rdr.GetValue(3)) ?? string.Empty,
                MethodName = rdr.IsDBNull(4) ? string.Empty : Convert.ToString(rdr.GetValue(4)) ?? string.Empty,
                EndpointPath = rdr.IsDBNull(5) ? string.Empty : Convert.ToString(rdr.GetValue(5)) ?? string.Empty,
                ApiName = rdr.IsDBNull(6) ? string.Empty : Convert.ToString(rdr.GetValue(6)) ?? string.Empty,
                UsageMonth = rdr.IsDBNull(7) ? 0 : Convert.ToInt32(rdr.GetValue(7)),
                HitCount = hitCount,
                MonthlyQuota = quota,
                RemainingQuota = remaining,
                UsagePercent = pct,
                LastHitDate = rdr.IsDBNull(10) ? null : rdr.GetDateTime(10),
                LastSyncedDate = rdr.IsDBNull(11) ? null : rdr.GetDateTime(11),
                SourceApiUrl = rdr.IsDBNull(12) ? null : Convert.ToString(rdr.GetValue(12))
            };
        }





    }
}
