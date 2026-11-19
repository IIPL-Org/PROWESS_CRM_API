using System.Data;
using Microsoft.Data.SqlClient;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Data
{
    public interface IUserRepository
    {
        Task<UserMaster?> GetByUsernameOrEmailAsync(string identifier);
        Task UpdateLastLoginAsync(int userId);
        Task<UserMaster?> GetByIdAsync(int userId);
        Task<IEnumerable<UserMaster>> GetAllUsersAsync();
        Task<UserMaster> CreateUserAsync(UserMaster user);
        Task<bool> UpdateUserAsync(UserMaster user);
    }

    public class UserRepository : IUserRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<UserMaster?> GetByUsernameOrEmailAsync(string identifier)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            // Uses SQL DecryptByPassPhrase to automatically decrypt SQL-encrypted PasswordHash binary values, handling both VARCHAR and UTF-16 NVARCHAR storage
            cmd.CommandText = @"
                SELECT TOP 1 
                    u.UserId, 
                    u.Username, 
                    u.Email, 
                    ISNULL(
                        CASE 
                            WHEN CHARINDEX(CHAR(0), CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS VARCHAR(500))) > 0
                            THEN CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS NVARCHAR(500))
                            ELSE CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS VARCHAR(500))
                        END,
                        CAST(u.PasswordHash AS NVARCHAR(500))
                    ) AS PasswordHash,
                    u.FullName, 
                    u.Role, 
                    u.IsActive, 
                    u.CreatedAt, 
                    u.LastLoginAt, 
                    u.LastLoginAt AS LastLogin,
                    (SELECT STRING_AGG(CAST(up.Product_Id AS VARCHAR(10)), ',') FROM dbo.User_Products up WHERE up.User_Id = u.UserId) AS AssignedProductIds,
                    (SELECT TOP 1 up.Product_Id FROM dbo.User_Products up WHERE up.User_Id = u.UserId AND up.Is_Default = 1) AS DefaultProductId
                FROM dbo.UserMaster u
                WHERE (u.Username = @Id OR u.Email = @Id) AND u.IsActive = 1;";

            var param = cmd.CreateParameter();
            param.ParameterName = "@Id";
            param.Value = identifier;
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapUserFromReader(reader);
            }

            return null;
        }

        public async Task<UserMaster?> GetByIdAsync(int userId)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT 
                    u.UserId, 
                    u.Username, 
                    u.Email, 
                    ISNULL(
                        CASE 
                            WHEN CHARINDEX(CHAR(0), CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS VARCHAR(500))) > 0
                            THEN CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS NVARCHAR(500))
                            ELSE CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS VARCHAR(500))
                        END,
                        CAST(u.PasswordHash AS NVARCHAR(500))
                    ) AS PasswordHash,
                    u.FullName, 
                    u.Role, 
                    u.IsActive, 
                    u.CreatedAt, 
                    u.LastLoginAt, 
                    u.LastLoginAt AS LastLogin,
                    (SELECT STRING_AGG(CAST(up.Product_Id AS VARCHAR(10)), ',') FROM dbo.User_Products up WHERE up.User_Id = u.UserId) AS AssignedProductIds,
                    (SELECT TOP 1 up.Product_Id FROM dbo.User_Products up WHERE up.User_Id = u.UserId AND up.Is_Default = 1) AS DefaultProductId
                FROM dbo.UserMaster u
                WHERE u.UserId = @UserId;";

            var param = cmd.CreateParameter();
            param.ParameterName = "@UserId";
            param.Value = userId;
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapUserFromReader(reader);
            }

            return null;
        }

        public async Task UpdateLastLoginAsync(int userId)
        {
            try
            {
                using var conn = await _connectionFactory.CreateConnectionAsync();
                using var cmd = conn.CreateCommand();

                cmd.CommandText = "UPDATE dbo.UserMaster SET LastLoginAt = GETUTCDATE() WHERE UserId = @UserId;";

                var param = cmd.CreateParameter();
                param.ParameterName = "@UserId";
                param.Value = userId;
                cmd.Parameters.Add(param);

                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Non-critical background update
            }
        }

        public async Task<IEnumerable<UserMaster>> GetAllUsersAsync()
        {
            var list = new List<UserMaster>();
            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                SELECT 
                    u.UserId, 
                    u.Username, 
                    u.Email, 
                    ISNULL(
                        CASE 
                            WHEN CHARINDEX(CHAR(0), CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS VARCHAR(500))) > 0
                            THEN CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS NVARCHAR(500))
                            ELSE CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', u.PasswordHash) AS VARCHAR(500))
                        END,
                        CAST(u.PasswordHash AS NVARCHAR(500))
                    ) AS PasswordHash,
                    u.FullName, 
                    u.Role, 
                    u.IsActive, 
                    u.CreatedAt, 
                    u.LastLoginAt, 
                    u.LastLoginAt AS LastLogin,
                    (SELECT STRING_AGG(CAST(up.Product_Id AS VARCHAR(10)), ',') FROM dbo.User_Products up WHERE up.User_Id = u.UserId) AS AssignedProductIds,
                    (SELECT TOP 1 up.Product_Id FROM dbo.User_Products up WHERE up.User_Id = u.UserId AND up.Is_Default = 1) AS DefaultProductId
                FROM dbo.UserMaster u
                ORDER BY u.UserId DESC;";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(MapUserFromReader(reader));
            }

            return list;
        }

        public async Task<UserMaster> CreateUserAsync(UserMaster user)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO dbo.UserMaster (Username, Email, PasswordHash, FullName, Role, IsActive, CreatedAt)
                VALUES (@Username, @Email, EncryptByPassPhrase('PayrollCRM_SecretKey_2026', CAST(@PasswordHash AS VARCHAR(500))), @FullName, @Role, @IsActive, GETUTCDATE());
                SELECT SCOPE_IDENTITY();";

            AddParam(cmd, "@Username", user.Username);
            AddParam(cmd, "@Email", user.Email);
            AddParam(cmd, "@PasswordHash", string.IsNullOrEmpty(user.PasswordHash) ? "123456" : user.PasswordHash);
            AddParam(cmd, "@FullName", user.FullName);
            AddParam(cmd, "@Role", user.Role ?? "User");
            AddParam(cmd, "@IsActive", user.IsActive ? 1 : 0);

            var scalar = await cmd.ExecuteScalarAsync();
            int newId = Convert.ToInt32(scalar);
            user.UserId = newId;

            // Assign product suites into dbo.User_Products
            var productIds = user.AssignedProductIds != null && user.AssignedProductIds.Count > 0
                ? user.AssignedProductIds
                : new List<int> { 1, 2, 3, 4 };

            int defaultId = user.DefaultProductId ?? productIds.FirstOrDefault();

            foreach (var prodId in productIds)
            {
                using var pCmd = conn.CreateCommand();
                pCmd.CommandText = @"
                    IF NOT EXISTS (SELECT 1 FROM dbo.User_Products WHERE User_Id = @UserId AND Product_Id = @ProductId)
                    BEGIN
                        INSERT INTO dbo.User_Products (User_Id, Product_Id, Is_Default, Assigned_Date)
                        VALUES (@UserId, @ProductId, @IsDefault, GETUTCDATE());
                    END";
                AddParam(pCmd, "@UserId", newId);
                AddParam(pCmd, "@ProductId", prodId);
                AddParam(pCmd, "@IsDefault", prodId == defaultId ? 1 : 0);
                await pCmd.ExecuteNonQueryAsync();
            }

            user.AssignedProductIds = productIds;
            user.DefaultProductId = defaultId;
            return user;
        }

        public async Task<bool> UpdateUserAsync(UserMaster user)
        {
            using var conn = await _connectionFactory.CreateConnectionAsync();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                UPDATE dbo.UserMaster 
                SET Username = @Username,
                    Email = @Email,
                    FullName = @FullName,
                    Role = @Role,
                    IsActive = @IsActive,
                    UpdatedAt = GETUTCDATE(),
                    PasswordHash = CASE 
                        WHEN @PasswordHash IS NOT NULL AND LEN(@PasswordHash) > 0 
                        THEN EncryptByPassPhrase('PayrollCRM_SecretKey_2026', CAST(@PasswordHash AS VARCHAR(500))) 
                        ELSE PasswordHash 
                    END
                WHERE UserId = @UserId;";

            AddParam(cmd, "@UserId", user.UserId);
            AddParam(cmd, "@Username", user.Username);
            AddParam(cmd, "@Email", user.Email);
            AddParam(cmd, "@PasswordHash", user.PasswordHash);
            AddParam(cmd, "@FullName", user.FullName);
            AddParam(cmd, "@Role", user.Role ?? "User");
            AddParam(cmd, "@IsActive", user.IsActive ? 1 : 0);

            int rows = await cmd.ExecuteNonQueryAsync();

            // Update assigned product suites if provided
            if (user.AssignedProductIds != null && user.AssignedProductIds.Count > 0)
            {
                using var delCmd = conn.CreateCommand();
                delCmd.CommandText = "DELETE FROM dbo.User_Products WHERE User_Id = @UserId;";
                AddParam(delCmd, "@UserId", user.UserId);
                await delCmd.ExecuteNonQueryAsync();

                int defaultId = user.DefaultProductId ?? user.AssignedProductIds.FirstOrDefault();

                foreach (var prodId in user.AssignedProductIds)
                {
                    using var pCmd = conn.CreateCommand();
                    pCmd.CommandText = @"
                        INSERT INTO dbo.User_Products (User_Id, Product_Id, Is_Default, Assigned_Date)
                        VALUES (@UserId, @ProductId, @IsDefault, GETUTCDATE());";
                    AddParam(pCmd, "@UserId", user.UserId);
                    AddParam(pCmd, "@ProductId", prodId);
                    AddParam(pCmd, "@IsDefault", prodId == defaultId ? 1 : 0);
                    await pCmd.ExecuteNonQueryAsync();
                }
            }

            return rows > 0;
        }

        private static void AddParam(System.Data.IDbCommand cmd, string name, object val)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = val ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static UserMaster MapUserFromReader(IDataRecord reader)
        {
            string passHash = GetStringSafe(reader, "PasswordHash");

            string prodIdsStr = GetStringSafe(reader, "AssignedProductIds");
            var prodIds = new List<int>();
            if (!string.IsNullOrWhiteSpace(prodIdsStr))
            {
                prodIds = prodIdsStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(s => int.TryParse(s.Trim(), out int id) ? id : 0)
                                    .Where(id => id > 0)
                                    .ToList();
            }

            return new UserMaster
            {
                UserId = GetIntSafe(reader, "UserId"),
                Username = GetStringSafe(reader, "Username"),
                Email = GetStringSafe(reader, "Email"),
                PasswordHash = passHash,
                FullName = GetStringSafe(reader, "FullName"),
                Role = GetStringSafe(reader, "Role", "User"),
                IsActive = GetBoolSafe(reader, "IsActive"),
                CreatedAt = GetDateTimeSafe(reader, "CreatedAt"),
                UpdatedAt = GetNullableDateTime(reader, "UpdatedAt"),
                LastLoginAt = GetNullableDateTime(reader, "LastLoginAt") ?? GetNullableDateTime(reader, "LastLogin"),
                LastLogin = GetNullableDateTime(reader, "LastLoginAt") ?? GetNullableDateTime(reader, "LastLogin"),
                AssignedProductIds = prodIds,
                DefaultProductId = GetNullableInt(reader, "DefaultProductId")
            };
        }

        private static int GetIntSafe(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? 0 : Convert.ToInt32(r.GetValue(ord)); }
            catch { return 0; }
        }

        private static string GetStringSafe(IDataRecord r, string col, string defaultVal = "")
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? defaultVal : r.GetString(ord); }
            catch { return defaultVal; }
        }

        private static bool GetBoolSafe(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return !r.IsDBNull(ord) && Convert.ToBoolean(r.GetValue(ord)); }
            catch { return false; }
        }

        private static DateTime GetDateTimeSafe(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? DateTime.UtcNow : Convert.ToDateTime(r.GetValue(ord)); }
            catch { return DateTime.UtcNow; }
        }

        private static DateTime? GetNullableDateTime(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? null : Convert.ToDateTime(r.GetValue(ord)); }
            catch { return null; }
        }

        private static int? GetNullableInt(IDataRecord r, string col)
        {
            try { int ord = r.GetOrdinal(col); return r.IsDBNull(ord) ? null : Convert.ToInt32(r.GetValue(ord)); }
            catch { return null; }
        }
    }
}
