using System.Data;
using Microsoft.Data.SqlClient;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Data
{
    public interface IUserMobileDetailRepository
    {
        Task<int> CreateAsync(UserMobileDetail detail, CancellationToken cancellationToken = default);
        Task<bool> UpdateAsync(UserMobileDetail detail, CancellationToken cancellationToken = default);
        Task<UserMobileDetail?> GetByIdAsync(int uMobId, CancellationToken cancellationToken = default);
        Task<UserMobileDetail?> GetByEmpIdAsync(int empId, CancellationToken cancellationToken = default);
        Task<UserMobileDetail?> GetByClientAndEmpIdAsync(int clientId, int empId, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserMobileDetail>> GetByClientIdAsync(int clientId, string? status = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<UserMobileDetail>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<UserValidationResponseDto?> ValidateUserMobileAsync(string mobileNo, CancellationToken cancellationToken = default);
        Task<bool> IsActiveEmpIdExistsAsync(int clientId, int empId, int? excludeUMobId = null, CancellationToken cancellationToken = default);
        Task<bool> IsActiveMobileNumberExistsAsync(string mobileNo, int? excludeUMobId = null, CancellationToken cancellationToken = default);
        Task<bool> IsClientActiveAsync(int clientId, CancellationToken cancellationToken = default);
        Task<bool> UpdateStatusAsync(int clientId, int empId, char isActive, int modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken = default);
    }

    public class UserMobileDetailRepository : IUserMobileDetailRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public UserMobileDetailRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateAsync(UserMobileDetail detail, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT INTO User_Mobile_Detail
                (
                    Client_Id, Emp_Id, Client_Mobile, Is_Active, Created_By, Created_Date
                )
                VALUES
                (
                    @Client_Id, @Emp_Id, @Client_Mobile, @Is_Active, @Created_By, @Created_Date
                );
                SELECT CAST(SCOPE_IDENTITY() as int);";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Client_Id", detail.ClientId);
            command.Parameters.AddWithValue("@Emp_Id", detail.EmpId);
            command.Parameters.AddWithValue("@Client_Mobile", detail.ClientMobile);
            command.Parameters.AddWithValue("@Is_Active", detail.IsActive);
            command.Parameters.AddWithValue("@Created_By", detail.CreatedBy);
            command.Parameters.AddWithValue("@Created_Date", detail.CreatedDate);

            var newId = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(newId);
        }

        public async Task<bool> UpdateAsync(UserMobileDetail detail, CancellationToken cancellationToken = default)
        {
            string sql;
            sql = @"
                    UPDATE User_Mobile_Detail
                    SET
                        Client_Mobile = @Client_Mobile,
                        Is_Active = @Is_Active,
                        Modified_By = @Modified_By,
                        Modified_Date = @Modified_Date
                    WHERE Client_Id = @Client_Id AND Emp_Id = @Emp_Id;";


            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Client_Id", detail.ClientId);
            command.Parameters.AddWithValue("@Emp_Id", detail.EmpId);
            command.Parameters.AddWithValue("@Client_Mobile", detail.ClientMobile ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Is_Active", detail.IsActive);
            command.Parameters.AddWithValue("@Modified_By", (object?)detail.ModifiedBy ?? DBNull.Value);
            command.Parameters.AddWithValue("@Modified_Date", (object?)detail.ModifiedDate ?? DBNull.Value);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            return rowsAffected > 0;
        }

        public async Task<UserMobileDetail?> GetByIdAsync(int uMobId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT UMob_Id, Client_Id, Emp_Id, Client_Mobile, Is_Active, Created_By, Created_Date, Modified_By, Modified_Date
                FROM User_Mobile_Detail
                WHERE UMob_Id = @UMob_Id;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@UMob_Id", uMobId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapReaderToEntity(reader);
            }

            return null;
        }

        public async Task<UserMobileDetail?> GetByEmpIdAsync(int empId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT TOP 1 UMob_Id, Client_Id, Emp_Id, Client_Mobile, Is_Active, Created_By, Created_Date, Modified_By, Modified_Date
                FROM User_Mobile_Detail
                WHERE Emp_Id = @Emp_Id;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Emp_Id", empId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapReaderToEntity(reader);
            }

            return null;
        }

        public async Task<UserMobileDetail?> GetByClientAndEmpIdAsync(int clientId, int empId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT TOP 1 UMob_Id, Client_Id, Emp_Id, Client_Mobile, Is_Active, Created_By, Created_Date, Modified_By, Modified_Date
                FROM User_Mobile_Detail
                WHERE Client_Id = @Client_Id AND Emp_Id = @Emp_Id;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Id", clientId);
            command.Parameters.AddWithValue("@Emp_Id", empId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return MapReaderToEntity(reader);
            }

            return null;
        }

        public async Task<IEnumerable<UserMobileDetail>> GetByClientIdAsync(int clientId, string? status = null, CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT UMob_Id, Client_Id, Emp_Id, Client_Mobile, Is_Active, Created_By, Created_Date, Modified_By, Modified_Date
                FROM User_Mobile_Detail
                WHERE Client_Id = @Client_Id";

            if (!string.IsNullOrWhiteSpace(status))
            {
                sql += " AND UPPER(Is_Active) = UPPER(@Status)";
            }

            sql += ";";

            var list = new List<UserMobileDetail>();
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Id", clientId);

            if (!string.IsNullOrWhiteSpace(status))
            {
                command.Parameters.AddWithValue("@Status", status);
            }

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapReaderToEntity(reader));
            }

            return list;
        }

        public async Task<IEnumerable<UserMobileDetail>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT UMob_Id, Client_Id, Emp_Id, Client_Mobile, Is_Active, Created_By, Created_Date, Modified_By, Modified_Date
                FROM User_Mobile_Detail;";

            var list = new List<UserMobileDetail>();
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(MapReaderToEntity(reader));
            }

            return list;
        }

        public async Task<UserValidationResponseDto?> ValidateUserMobileAsync(string mobileNo, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT TOP 1
                    umd.Client_Id,
                    umd.Emp_Id,
                    cm.Client_Name,
                    cm.HRMS_API_Link
                FROM User_Mobile_Detail umd
                INNER JOIN Client_Master cm ON umd.Client_Id = cm.Client_Id
                WHERE umd.Client_Mobile = @Client_Mobile
                  AND UPPER(umd.Is_Active) = 'Y'
                  AND UPPER(cm.Is_Active) = 'Y';";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Mobile", mobileNo);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new UserValidationResponseDto
                {
                    Status = "Y",
                    Message = "User mobile validation successful.",
                    IsExist = true,
                    ClientId = reader.GetInt32(reader.GetOrdinal("Client_Id")),
                    ClientName = reader.IsDBNull(reader.GetOrdinal("Client_Name")) ? null : reader.GetString(reader.GetOrdinal("Client_Name")),
                    EmpId = reader.GetInt32(reader.GetOrdinal("Emp_Id")),
                    HrmsApiLink = reader.IsDBNull(reader.GetOrdinal("HRMS_API_Link")) ? null : reader.GetString(reader.GetOrdinal("HRMS_API_Link"))
                };
            }

            return null;
        }

        public async Task<bool> IsActiveEmpIdExistsAsync(int clientId, int empId, int? excludeUMobId = null, CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM User_Mobile_Detail 
                WHERE Client_Id = @Client_Id
                  AND Emp_Id = @Emp_Id 
                  AND UPPER(Is_Active) = 'Y'";

            if (excludeUMobId.HasValue)
            {
                sql += " AND UMob_Id <> @ExcludeUMobId";
            }

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Id", clientId);
            command.Parameters.AddWithValue("@Emp_Id", empId);

            if (excludeUMobId.HasValue)
            {
                command.Parameters.AddWithValue("@ExcludeUMobId", excludeUMobId.Value);
            }

            var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            return count > 0;
        }

        public async Task<bool> IsActiveMobileNumberExistsAsync(string mobileNo, int? excludeUMobId = null, CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM User_Mobile_Detail 
                WHERE Client_Mobile = @Client_Mobile 
                  AND UPPER(Is_Active) = 'Y'";

            if (excludeUMobId.HasValue)
            {
                sql += " AND UMob_Id <> @ExcludeUMobId";
            }

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Mobile", mobileNo.Trim());

            if (excludeUMobId.HasValue)
            {
                command.Parameters.AddWithValue("@ExcludeUMobId", excludeUMobId.Value);
            }

            var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            return count > 0;
        }

        public async Task<bool> IsClientActiveAsync(int clientId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT COUNT(1) 
                FROM Client_Master 
                WHERE Client_Id = @Client_Id 
                  AND UPPER(Is_Active) = 'Y';";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Id", clientId);

            var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            return count > 0;
        }

        public async Task<bool> UpdateStatusAsync(int clientId, int empId, char isActive, int modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE User_Mobile_Detail
                SET
                    Is_Active = @Is_Active,
                    Modified_By = @Modified_By,
                    Modified_Date = @Modified_Date
                WHERE Client_Id = @Client_Id AND Emp_Id = @Emp_Id;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Client_Id", clientId);
            command.Parameters.AddWithValue("@Emp_Id", empId);
            command.Parameters.AddWithValue("@Is_Active", isActive.ToString());
            command.Parameters.AddWithValue("@Modified_By", modifiedBy);
            command.Parameters.AddWithValue("@Modified_Date", modifiedDate);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            return rowsAffected > 0;
        }

        private static UserMobileDetail MapReaderToEntity(SqlDataReader reader)
        {
            return new UserMobileDetail
            {
                UMobId = reader.GetInt32(reader.GetOrdinal("UMob_Id")),
                ClientId = reader.GetInt32(reader.GetOrdinal("Client_Id")),
                EmpId = reader.GetInt32(reader.GetOrdinal("Emp_Id")),
                ClientMobile = reader.GetString(reader.GetOrdinal("Client_Mobile")),
                IsActive = Convert.ToChar(reader.GetString(reader.GetOrdinal("Is_Active"))),
                CreatedBy = reader.GetInt32(reader.GetOrdinal("Created_By")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("Created_Date")),
                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("Modified_By")) ? null : reader.GetInt32(reader.GetOrdinal("Modified_By")),
                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("Modified_Date")) ? null : reader.GetDateTime(reader.GetOrdinal("Modified_Date"))
            };
        }
    }
}
