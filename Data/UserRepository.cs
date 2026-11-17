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

            // Uses SQL DecryptByPassPhrase to automatically decrypt SQL-encrypted PasswordHash binary values
            cmd.CommandText = @"
                SELECT TOP 1 
                    UserId, 
                    Username, 
                    Email, 
                    ISNULL(
                        CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', PasswordHash) AS VARCHAR(500)),
                        CAST(PasswordHash AS VARCHAR(500))
                    ) AS PasswordHash,
                    FullName, 
                    Role, 
                    IsActive, 
                    CreatedAt, 
                    LastLoginAt, 
                    LastLoginAt AS LastLogin
                FROM dbo.UserMaster
                WHERE (Username = @Id OR Email = @Id) AND IsActive = 1;";

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
                    UserId, 
                    Username, 
                    Email, 
                    ISNULL(
                        CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', PasswordHash) AS VARCHAR(500)),
                        CAST(PasswordHash AS VARCHAR(500))
                    ) AS PasswordHash,
                    FullName, 
                    Role, 
                    IsActive, 
                    CreatedAt, 
                    LastLoginAt, 
                    LastLoginAt AS LastLogin
                FROM dbo.UserMaster
                WHERE UserId = @UserId;";

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
                    UserId, 
                    Username, 
                    Email, 
                    ISNULL(
                        CAST(DecryptByPassPhrase('PayrollCRM_SecretKey_2026', PasswordHash) AS VARCHAR(500)),
                        CAST(PasswordHash AS VARCHAR(500))
                    ) AS PasswordHash,
                    FullName, 
                    Role, 
                    IsActive, 
                    CreatedAt, 
                    LastLoginAt, 
                    LastLoginAt AS LastLogin
                FROM dbo.UserMaster
                ORDER BY UserId DESC;";

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
                VALUES (@Username, @Email, EncryptByPassPhrase('PayrollCRM_SecretKey_2026', @PasswordHash), @FullName, @Role, @IsActive, GETUTCDATE());
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
                    UpdatedAt = GETUTCDATE()
                WHERE UserId = @UserId;";

            AddParam(cmd, "@UserId", user.UserId);
            AddParam(cmd, "@Username", user.Username);
            AddParam(cmd, "@Email", user.Email);
            AddParam(cmd, "@FullName", user.FullName);
            AddParam(cmd, "@Role", user.Role ?? "User");
            AddParam(cmd, "@IsActive", user.IsActive ? 1 : 0);

            int rows = await cmd.ExecuteNonQueryAsync();
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
                LastLogin = GetNullableDateTime(reader, "LastLoginAt") ?? GetNullableDateTime(reader, "LastLogin")
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
    }
}
