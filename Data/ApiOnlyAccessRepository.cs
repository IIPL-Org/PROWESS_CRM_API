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
    }
}
