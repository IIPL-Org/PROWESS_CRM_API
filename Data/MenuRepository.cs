using System.Data;
using Microsoft.Data.SqlClient;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Data
{
    public interface IMenuRepository
    {
        Task<IEnumerable<MenuMaster>> GetMenusByRoleAsync(string roleName, string? productCode = null);
        Task<IEnumerable<MenuMaster>> GetAllMenusAsync(string? productCode = null);
    }

    public class MenuRepository : IMenuRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public MenuRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<MenuMaster>> GetMenusByRoleAsync(string roleName, string? productCode = null)
        {
            var menus = new List<MenuMaster>();

            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT 
                    MenuId,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'MenuLabel') IS NOT NULL THEN MenuLabel ELSE Title END AS Title,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'MenuPath') IS NOT NULL THEN MenuPath ELSE Route END AS Route,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'MenuIcon') IS NOT NULL THEN MenuIcon ELSE IconName END AS IconName,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'ParentMenuId') IS NOT NULL THEN ParentMenuId ELSE ParentId END AS ParentId,
                    SortOrder,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'AllowedRoles') IS NOT NULL THEN AllowedRoles ELSE Roles END AS Roles,
                    IsActive,
                    'Main' AS Category,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'Product_Code') IS NOT NULL THEN Product_Code ELSE NULL END AS Product_Code,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'Is_Common') IS NOT NULL THEN Is_Common ELSE CAST(0 AS BIT) END AS Is_Common
                FROM dbo.MenuMaster
                WHERE IsActive = 1
                  AND (@RoleName IS NULL OR AllowedRoles IS NULL OR AllowedRoles LIKE '%' + @RoleName + '%')
                  AND (@ProductCode IS NULL OR Is_Common = 1 OR Product_Code IS NULL OR Product_Code = @ProductCode)
                ORDER BY SortOrder ASC;";

            var paramRole = cmd.CreateParameter();
            paramRole.ParameterName = "@RoleName";
            paramRole.Value = string.IsNullOrWhiteSpace(roleName) ? (object)DBNull.Value : roleName.Trim();
            cmd.Parameters.Add(paramRole);

            var paramProd = cmd.CreateParameter();
            paramProd.ParameterName = "@ProductCode";
            paramProd.Value = string.IsNullOrWhiteSpace(productCode) ? (object)DBNull.Value : productCode.Trim();
            cmd.Parameters.Add(paramProd);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                menus.Add(MapMenuFromReader(reader));
            }

            return menus;
        }

        public async Task<IEnumerable<MenuMaster>> GetAllMenusAsync(string? productCode = null)
        {
            var menus = new List<MenuMaster>();

            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT 
                    MenuId,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'MenuLabel') IS NOT NULL THEN MenuLabel ELSE Title END AS Title,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'MenuPath') IS NOT NULL THEN MenuPath ELSE Route END AS Route,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'MenuIcon') IS NOT NULL THEN MenuIcon ELSE IconName END AS IconName,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'ParentMenuId') IS NOT NULL THEN ParentMenuId ELSE ParentId END AS ParentId,
                    SortOrder,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'AllowedRoles') IS NOT NULL THEN AllowedRoles ELSE Roles END AS Roles,
                    IsActive,
                    'Main' AS Category,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'Product_Code') IS NOT NULL THEN Product_Code ELSE NULL END AS Product_Code,
                    CASE WHEN COL_LENGTH('dbo.MenuMaster', 'Is_Common') IS NOT NULL THEN Is_Common ELSE CAST(0 AS BIT) END AS Is_Common
                FROM dbo.MenuMaster
                WHERE IsActive = 1
                ORDER BY SortOrder ASC;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                menus.Add(MapMenuFromReader(reader));
            }

            if (!string.IsNullOrWhiteSpace(productCode))
            {
                menus = menus.Where(m => m.IsCommon || string.IsNullOrEmpty(m.ProductCode) || string.Equals(m.ProductCode, productCode, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return menus;
        }

        private static MenuMaster MapMenuFromReader(IDataRecord reader)
        {
            string title = GetStringSafe(reader, "Title");
            if (string.IsNullOrEmpty(title)) title = GetStringSafe(reader, "MenuLabel");
            if (string.IsNullOrEmpty(title)) title = GetStringSafe(reader, "MenuName");

            string route = GetStringSafe(reader, "Route");
            if (string.IsNullOrEmpty(route)) route = GetStringSafe(reader, "MenuPath");

            string iconName = GetStringSafe(reader, "IconName");
            if (string.IsNullOrEmpty(iconName)) iconName = GetStringSafe(reader, "MenuIcon");

            int? parentId = GetNullableInt(reader, "ParentId") ?? GetNullableInt(reader, "ParentMenuId");

            string roles = GetStringSafe(reader, "Roles");
            if (string.IsNullOrEmpty(roles)) roles = GetStringSafe(reader, "AllowedRoles", "Admin,HR,Manager,User");

            string? productCode = GetNullableString(reader, "ProductCode") ?? GetNullableString(reader, "Product_Code");
            bool isCommon = GetBoolSafe(reader, "IsCommon") || GetBoolSafe(reader, "Is_Common");

            return new MenuMaster
            {
                MenuId = GetIntSafe(reader, "MenuId"),
                Title = title,
                Route = route,
                IconName = iconName,
                ParentId = parentId,
                SortOrder = GetIntSafe(reader, "SortOrder"),
                Roles = roles,
                IsActive = GetBoolSafe(reader, "IsActive"),
                Category = GetStringSafe(reader, "Category", "Main"),
                ProductCode = productCode,
                IsCommon = isCommon
            };
        }

        private static int GetIntSafe(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? 0 : Convert.ToInt32(r.GetValue(ord)); }
            catch { return 0; }
        }

        private static int? GetNullableInt(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? null : Convert.ToInt32(r.GetValue(ord)); }
            catch { return null; }
        }

        private static string GetStringSafe(IDataRecord r, string col, string defaultVal = "")
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? defaultVal : r.GetString(ord); }
            catch { return defaultVal; }
        }

        private static string? GetNullableString(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? null : r.GetString(ord); }
            catch { return null; }
        }

        private static bool GetBoolSafe(IDataRecord r, string col)
        {
            try
            {
                int ord = r.GetOrdinal(col);
                if (r.IsDBNull(ord)) return false;
                var val = r.GetValue(ord);
                if (val is bool b) return b;
                if (val is int i) return i == 1;
                var s = val.ToString()?.Trim().ToUpper();
                return s == "1" || s == "TRUE" || s == "Y";
            }
            catch { return false; }
        }
    }
}
