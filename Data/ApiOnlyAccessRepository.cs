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
    }
}
