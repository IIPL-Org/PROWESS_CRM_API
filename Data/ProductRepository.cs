using System.Data;
using Microsoft.Data.SqlClient;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Data
{
    public interface IProductRepository
    {
        Task<IEnumerable<ProductMaster>> GetAllActiveProductsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<ProductMaster>> GetProductsByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    }

    public class ProductRepository : IProductRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly ILogger<ProductRepository> _logger;

        // Default fallback suites matching official product portfolio
        private static readonly List<ProductMaster> DefaultProducts = new()
        {
            new ProductMaster { ProductId = 1, ProductCode = "ADMIN", ProductName = "Admin Suit", Description = "Central Administration, Client Management & Licensing", IconName = "MdAdminPanelSettings", ThemeColor = "#2563eb", SortOrder = 1, IsActive = true },
            new ProductMaster { ProductId = 2, ProductCode = "HRMS", ProductName = "Prowess HRMS Connect", Description = "Human Resource Management, Workforce Ops & Payroll Processing", IconName = "MdPeople", ThemeColor = "#059669", SortOrder = 2, IsActive = true },
            new ProductMaster { ProductId = 3, ProductCode = "ERP", ProductName = "Prowess ERP Connect", Description = "Enterprise Resource Planning, Accounts & Business Operations", IconName = "MdPayments", ThemeColor = "#7c3aed", SortOrder = 3, IsActive = true },
            new ProductMaster { ProductId = 4, ProductCode = "TICKET", ProductName = "Prowess Ticket", Description = "Helpdesk, Ticketing & Support Management", IconName = "MdConfirmationNumber", ThemeColor = "#d97706", SortOrder = 4, IsActive = true }
        };

        public ProductRepository(IDbConnectionFactory connectionFactory, ILogger<ProductRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<IEnumerable<ProductMaster>> GetAllActiveProductsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var cmd = conn.CreateCommand();

                cmd.CommandText = @"
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Product_Master]') AND type in (N'U'))
                    BEGIN
                        SELECT Product_Id, Product_Code, Product_Name, Description, Icon_Name, Theme_Color, Sort_Order, Is_Active
                        FROM dbo.Product_Master
                        WHERE Is_Active = 1
                        ORDER BY Sort_Order ASC;
                    END
                    ELSE
                    BEGIN
                        SELECT 0 AS Product_Id, '' AS Product_Code, '' AS Product_Name, '' AS Description, '' AS Icon_Name, '' AS Theme_Color, 0 AS Sort_Order, 0 AS Is_Active
                        WHERE 1 = 0;
                    END";

                var list = new List<ProductMaster>();
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(MapProduct(reader));
                }

                return list.Count > 0 ? list : DefaultProducts;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query Product_Master from database. Returning default product suite list.");
                return DefaultProducts;
            }
        }

        public async Task<IEnumerable<ProductMaster>> GetProductsByUserIdAsync(int userId, CancellationToken cancellationToken = default)
        {
            try
            {
                using var conn = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var cmd = conn.CreateCommand();

                cmd.CommandText = @"
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[User_Products]') AND type in (N'U'))
                    BEGIN
                        SELECT p.Product_Id, p.Product_Code, p.Product_Name, p.Description, p.Icon_Name, p.Theme_Color, p.Sort_Order, p.Is_Active
                        FROM dbo.User_Products up
                        INNER JOIN dbo.Product_Master p ON up.Product_Id = p.Product_Id
                        WHERE up.User_Id = @UserId AND p.Is_Active = 1
                        ORDER BY p.Sort_Order ASC;
                    END
                    ELSE
                    BEGIN
                        SELECT 0 AS Product_Id, '' AS Product_Code, '' AS Product_Name, '' AS Description, '' AS Icon_Name, '' AS Theme_Color, 0 AS Sort_Order, 0 AS Is_Active
                        WHERE 1 = 0;
                    END";

                var param = cmd.CreateParameter();
                param.ParameterName = "@UserId";
                param.Value = userId;
                cmd.Parameters.Add(param);

                var list = new List<ProductMaster>();
                using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    list.Add(MapProduct(reader));
                }

                // If user doesn't have specific rows mapped in User_Products yet, default to all active products
                return list.Count > 0 ? list : await GetAllActiveProductsAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to query User_Products for UserId {UserId}. Returning all active products.", userId);
                return await GetAllActiveProductsAsync(cancellationToken);
            }
        }

        private static ProductMaster MapProduct(IDataRecord reader)
        {
            return new ProductMaster
            {
                ProductId = GetIntSafe(reader, "Product_Id"),
                ProductCode = GetStringSafe(reader, "Product_Code"),
                ProductName = GetStringSafe(reader, "Product_Name"),
                Description = GetNullableString(reader, "Description"),
                IconName = GetNullableString(reader, "Icon_Name"),
                ThemeColor = GetStringSafe(reader, "Theme_Color", "#3b82f6"),
                SortOrder = GetIntSafe(reader, "Sort_Order"),
                IsActive = GetBoolSafe(reader, "Is_Active")
            };
        }

        private static int GetIntSafe(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? 0 : Convert.ToInt32(r.GetValue(ord)); }
            catch { return 0; }
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
    }
}

