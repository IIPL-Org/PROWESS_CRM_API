using System.Data;
using Microsoft.Data.SqlClient;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Data
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

            // Try running procedure sp_GetMenusByRoleAndProduct if available; otherwise fallback to sp_GetMenusByRole or SQL
            cmd.CommandText = @"
                IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetMenusByRoleAndProduct]') AND type in (N'P', N'PC'))
                BEGIN
                    EXEC [dbo].[sp_GetMenusByRoleAndProduct] @RoleName = @RoleName, @ProductCode = @ProductCode;
                END
                ELSE IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_GetMenusByRole]') AND type in (N'P', N'PC'))
                BEGIN
                    EXEC [dbo].[sp_GetMenusByRole] @RoleName = @RoleName;
                END
                ELSE
                BEGIN
                    SELECT 
                        MenuId, Title, Route, IconName, ParentId, SortOrder, Roles, IsActive, Category
                    FROM dbo.MenuMaster
                    WHERE IsActive = 1
                    ORDER BY SortOrder ASC;
                END";

            var paramRole = cmd.CreateParameter();
            paramRole.ParameterName = "@RoleName";
            paramRole.Value = roleName ?? (object)DBNull.Value;
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

            // In-memory safety filter in case the legacy SP was used
            if (!string.IsNullOrWhiteSpace(productCode) && menus.Any(m => !string.IsNullOrEmpty(m.ProductCode) || m.IsCommon))
            {
                menus = menus.Where(m => m.IsCommon || string.IsNullOrEmpty(m.ProductCode) || string.Equals(m.ProductCode, productCode, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return menus;
        }

        public async Task<IEnumerable<MenuMaster>> GetAllMenusAsync(string? productCode = null)
        {
            var menus = new List<MenuMaster>();

            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT MenuId, Title, Route, IconName, ParentId, SortOrder, Roles, IsActive, Category,
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
            return new MenuMaster
            {
                MenuId = GetIntSafe(reader, "MenuId"),
                Title = GetStringSafe(reader, "Title"),
                Route = GetStringSafe(reader, "Route"),
                IconName = GetStringSafe(reader, "IconName"),
                ParentId = GetNullableInt(reader, "ParentId"),
                SortOrder = GetIntSafe(reader, "SortOrder"),
                Roles = GetStringSafe(reader, "Roles", "Admin,HR,Manager,User"),
                IsActive = GetBoolSafe(reader, "IsActive"),
                Category = GetStringSafe(reader, "Category", "Main"),
                ProductCode = GetNullableString(reader, "Product_Code"),
                IsCommon = GetBoolSafe(reader, "Is_Common")
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
