using System.Data;
using Microsoft.Data.SqlClient;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Data
{
    public interface IClientRepository
    {
        Task<int> CreateClientAsync(ClientMaster client, CancellationToken cancellationToken = default);
        Task<bool> UpdateClientAsync(ClientMaster client, CancellationToken cancellationToken = default);
        Task<ClientMaster?> GetClientByIdAsync(int clientId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ClientMaster>> GetAllClientsAsync(CancellationToken cancellationToken = default);
        Task<bool> ClientExistsByNameAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default);
        Task<bool> IsActiveClientNameExistsAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default);
        Task SaveClientModulesAsync(int clientId, List<int> moduleIds, CancellationToken cancellationToken = default);
        Task<List<int>> GetClientModulesAsync(int clientId, CancellationToken cancellationToken = default);
        Task<Dictionary<int, List<int>>> GetAllClientModulesAsync(CancellationToken cancellationToken = default);
        Task SaveClientESModulesAsync(int clientId, List<int> moduleIds, CancellationToken cancellationToken = default);
        Task<List<int>> GetClientESModulesAsync(int clientId, CancellationToken cancellationToken = default);
        Task<Dictionary<int, List<int>>> GetAllClientESModulesAsync(CancellationToken cancellationToken = default);
    }

    public class ClientRepository : IClientRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ClientRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<int> CreateClientAsync(ClientMaster client, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                INSERT INTO Client_Master
                (
                    Client_Name, Client_Address, HRMS_Admin_Link, HRMS_API_Link, HRMS_ESS_Link,
                    HRMS_Admin_Access, HRMS_API_Access, HRMS_ESS_Access, HRMS_App_Access,
                    Is_Active, Created_By, Created_Date
                )
                VALUES
                (
                    @Client_Name, @Client_Address, @HRMS_Admin_Link, @HRMS_API_Link, @HRMS_ESS_Link,
                    @HRMS_Admin_Access, @HRMS_API_Access, @HRMS_ESS_Access, @HRMS_App_Access,
                    @Is_Active, @Created_By, @Created_Date
                );
                SELECT CAST(SCOPE_IDENTITY() as int);";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            AddClientParameters(command, client);

            var newId = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(newId);
        }

        public async Task<bool> UpdateClientAsync(ClientMaster client, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                UPDATE Client_Master
                SET
                    Client_Name = @Client_Name,
                    Client_Address = @Client_Address,
                    HRMS_Admin_Link = @HRMS_Admin_Link,
                    HRMS_API_Link = @HRMS_API_Link,
                    HRMS_ESS_Link = @HRMS_ESS_Link,
                    HRMS_Admin_Access = @HRMS_Admin_Access,
                    HRMS_API_Access = @HRMS_API_Access,
                    HRMS_ESS_Access = @HRMS_ESS_Access,
                    HRMS_App_Access = @HRMS_App_Access,
                    Is_Active = @Is_Active,
                    Modified_By = @Modified_By,
                    Modified_Date = @Modified_Date
                WHERE Client_Id = @Client_Id;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Client_Id", client.ClientId);
            AddClientParameters(command, client);
            command.Parameters.AddWithValue("@Modified_By", (object?)client.ModifiedBy ?? DBNull.Value);
            command.Parameters.AddWithValue("@Modified_Date", (object?)client.ModifiedDate ?? DBNull.Value);

            int rowsAffected = await command.ExecuteNonQueryAsync(cancellationToken);
            return rowsAffected > 0;
        }

        public async Task<ClientMaster?> GetClientByIdAsync(int clientId, CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT 
                    Client_Id, Client_Name, Client_Address, HRMS_Admin_Link, HRMS_API_Link, HRMS_ESS_Link,
                    HRMS_Admin_Access, HRMS_API_Access, HRMS_ESS_Access, HRMS_App_Access,
                    Is_Active, Created_By, Created_Date, Modified_By, Modified_Date
                FROM Client_Master
                WHERE Client_Id = @Client_Id;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Id", clientId);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var clientMaster = MapClientFromReader(reader);
                reader.Close();
                clientMaster.ModuleIds = await GetClientModulesAsync(clientId, cancellationToken);
                clientMaster.EsModuleIds = await GetClientESModulesAsync(clientId, cancellationToken);
                return clientMaster;
            }

            return null;
        }

        public async Task<IEnumerable<ClientMaster>> GetAllClientsAsync(CancellationToken cancellationToken = default)
        {
            var clients = new List<ClientMaster>();
            const string sql = @"
                SELECT 
                    Client_Id, Client_Name, Client_Address, HRMS_Admin_Link, HRMS_API_Link, HRMS_ESS_Link,
                    HRMS_Admin_Access, HRMS_API_Access, HRMS_ESS_Access, HRMS_App_Access,
                    Is_Active, Created_By, Created_Date, Modified_By, Modified_Date
                FROM Client_Master
                ORDER BY Client_Id DESC;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                clients.Add(MapClientFromReader(reader));
            }

            reader.Close();

            var clientModules = await GetAllClientModulesAsync(cancellationToken);
            var clientEsModules = await GetAllClientESModulesAsync(cancellationToken);
            foreach (var client in clients)
            {
                if (clientModules.TryGetValue(client.ClientId, out var moduleIds))
                {
                    client.ModuleIds = moduleIds;
                }
                if (clientEsModules.TryGetValue(client.ClientId, out var esModuleIds))
                {
                    client.EsModuleIds = esModuleIds;
                }
            }

            return clients;
        }

        public async Task<bool> ClientExistsByNameAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM Client_Master 
                WHERE LOWER(Client_Name) = LOWER(@Client_Name)";

            if (excludeClientId.HasValue)
            {
                sql += " AND Client_Id <> @ExcludeClientId";
            }

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Name", clientName.Trim());

            if (excludeClientId.HasValue)
            {
                command.Parameters.AddWithValue("@ExcludeClientId", excludeClientId.Value);
            }

            var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            return count > 0;
        }

        public async Task<bool> IsActiveClientNameExistsAsync(string clientName, int? excludeClientId = null, CancellationToken cancellationToken = default)
        {
            string sql = @"
                SELECT COUNT(1) 
                FROM Client_Master 
                WHERE LOWER(Client_Name) = LOWER(@Client_Name)
                  AND UPPER(Is_Active) = 'Y'";

            if (excludeClientId.HasValue)
            {
                sql += " AND Client_Id <> @ExcludeClientId";
            }

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Client_Name", clientName.Trim());

            if (excludeClientId.HasValue)
            {
                command.Parameters.AddWithValue("@ExcludeClientId", excludeClientId.Value);
            }

            var count = (int)(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
            return count > 0;
        }

        private static void AddClientParameters(SqlCommand command, ClientMaster client)
        {
            command.Parameters.AddWithValue("@Client_Name", client.ClientName);
            command.Parameters.AddWithValue("@Client_Address", (object?)client.ClientAddress ?? DBNull.Value);
            command.Parameters.AddWithValue("@HRMS_Admin_Link", (object?)client.HrmsAdminLink ?? DBNull.Value);
            command.Parameters.AddWithValue("@HRMS_API_Link", (object?)client.HrmsApiLink ?? DBNull.Value);
            command.Parameters.AddWithValue("@HRMS_ESS_Link", (object?)client.HrmsEssLink ?? DBNull.Value);
            command.Parameters.AddWithValue("@HRMS_Admin_Access", client.HrmsAdminAccess.ToString());
            command.Parameters.AddWithValue("@HRMS_API_Access", client.HrmsApiAccess.ToString());
            command.Parameters.AddWithValue("@HRMS_ESS_Access", client.HrmsEssAccess.ToString());
            command.Parameters.AddWithValue("@HRMS_App_Access", client.HrmsAppAccess.ToString());
            command.Parameters.AddWithValue("@Is_Active", client.IsActive.ToString());
            command.Parameters.AddWithValue("@Created_By", client.CreatedBy);
            command.Parameters.AddWithValue("@Created_Date", client.CreatedDate);
        }

        private static ClientMaster MapClientFromReader(SqlDataReader reader)
        {
            return new ClientMaster
            {
                ClientId = reader.GetInt32(reader.GetOrdinal("Client_Id")),
                ClientName = reader.GetString(reader.GetOrdinal("Client_Name")),
                ClientAddress = reader.IsDBNull(reader.GetOrdinal("Client_Address")) ? null : reader.GetString(reader.GetOrdinal("Client_Address")),
                HrmsAdminLink = reader.IsDBNull(reader.GetOrdinal("HRMS_Admin_Link")) ? null : reader.GetString(reader.GetOrdinal("HRMS_Admin_Link")),
                HrmsApiLink = reader.IsDBNull(reader.GetOrdinal("HRMS_API_Link")) ? null : reader.GetString(reader.GetOrdinal("HRMS_API_Link")),
                HrmsEssLink = reader.IsDBNull(reader.GetOrdinal("HRMS_ESS_Link")) ? null : reader.GetString(reader.GetOrdinal("HRMS_ESS_Link")),
                HrmsAdminAccess = reader.IsDBNull(reader.GetOrdinal("HRMS_Admin_Access")) ? 'N' : reader.GetString(reader.GetOrdinal("HRMS_Admin_Access"))[0],
                HrmsApiAccess = reader.IsDBNull(reader.GetOrdinal("HRMS_API_Access")) ? 'N' : reader.GetString(reader.GetOrdinal("HRMS_API_Access"))[0],
                HrmsEssAccess = reader.IsDBNull(reader.GetOrdinal("HRMS_ESS_Access")) ? 'N' : reader.GetString(reader.GetOrdinal("HRMS_ESS_Access"))[0],
                HrmsAppAccess = reader.IsDBNull(reader.GetOrdinal("HRMS_App_Access")) ? 'N' : reader.GetString(reader.GetOrdinal("HRMS_App_Access"))[0],
                IsActive = reader.IsDBNull(reader.GetOrdinal("Is_Active")) ? 'N' : reader.GetString(reader.GetOrdinal("Is_Active"))[0],
                CreatedBy = reader.GetInt32(reader.GetOrdinal("Created_By")),
                CreatedDate = reader.GetDateTime(reader.GetOrdinal("Created_Date")),
                ModifiedBy = reader.IsDBNull(reader.GetOrdinal("Modified_By")) ? null : reader.GetInt32(reader.GetOrdinal("Modified_By")),
                ModifiedDate = reader.IsDBNull(reader.GetOrdinal("Modified_Date")) ? null : reader.GetDateTime(reader.GetOrdinal("Modified_Date"))
            };
        }

        public async Task SaveClientModulesAsync(int clientId, List<int> moduleIds, CancellationToken cancellationToken = default)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            
            // First delete existing mappings
            using var deleteCommand = new SqlCommand("DELETE FROM Client_Modules WHERE Client_Id = @Client_Id", connection);
            deleteCommand.Parameters.AddWithValue("@Client_Id", clientId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

            // Insert new mappings
            if (moduleIds != null && moduleIds.Any())
            {
                foreach (var moduleId in moduleIds)
                {
                    using var insertCommand = new SqlCommand("INSERT INTO Client_Modules (Client_Id, Module_Id) VALUES (@Client_Id, @Module_Id)", connection);
                    insertCommand.Parameters.AddWithValue("@Client_Id", clientId);
                    insertCommand.Parameters.AddWithValue("@Module_Id", moduleId);
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        public async Task<List<int>> GetClientModulesAsync(int clientId, CancellationToken cancellationToken = default)
        {
            var moduleIds = new List<int>();
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand("SELECT Module_Id FROM Client_Modules WHERE Client_Id = @Client_Id", connection);
            command.Parameters.AddWithValue("@Client_Id", clientId);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                moduleIds.Add(reader.GetInt32(0));
            }
            return moduleIds;
        }

        public async Task<Dictionary<int, List<int>>> GetAllClientModulesAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<int, List<int>>();
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand("SELECT Client_Id, Module_Id FROM Client_Modules", connection);
            
            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                int clientId = reader.GetInt32(0);
                int moduleId = reader.GetInt32(1);
                
                if (!result.ContainsKey(clientId))
                {
                    result[clientId] = new List<int>();
                }
                result[clientId].Add(moduleId);
            }
            return result;
        }

        public async Task SaveClientESModulesAsync(int clientId, List<int> moduleIds, CancellationToken cancellationToken = default)
        {
            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            
            // First delete existing mappings
            using var deleteCommand = new SqlCommand("DELETE FROM Client_ES_Modules WHERE Client_Id = @Client_Id", connection);
            deleteCommand.Parameters.AddWithValue("@Client_Id", clientId);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

            // Insert new mappings
            if (moduleIds != null && moduleIds.Any())
            {
                foreach (var moduleId in moduleIds)
                {
                    using var insertCommand = new SqlCommand("INSERT INTO Client_ES_Modules (Client_Id, Module_Id) VALUES (@Client_Id, @Module_Id)", connection);
                    insertCommand.Parameters.AddWithValue("@Client_Id", clientId);
                    insertCommand.Parameters.AddWithValue("@Module_Id", moduleId);
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
        }

        public async Task<List<int>> GetClientESModulesAsync(int clientId, CancellationToken cancellationToken = default)
        {
            var moduleIds = new List<int>();
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var command = new SqlCommand("SELECT Module_Id FROM Client_ES_Modules WHERE Client_Id = @Client_Id", connection);
                command.Parameters.AddWithValue("@Client_Id", clientId);
                
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    moduleIds.Add(reader.GetInt32(0));
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Table Client_ES_Modules does not exist yet (pending setup script execution)
                return new List<int>();
            }
            return moduleIds;
        }

        public async Task<Dictionary<int, List<int>>> GetAllClientESModulesAsync(CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<int, List<int>>();
            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var command = new SqlCommand("SELECT Client_Id, Module_Id FROM Client_ES_Modules", connection);
                
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    int clientId = reader.GetInt32(0);
                    int moduleId = reader.GetInt32(1);
                    
                    if (!result.ContainsKey(clientId))
                    {
                        result[clientId] = new List<int>();
                    }
                    result[clientId].Add(moduleId);
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Table Client_ES_Modules does not exist yet (pending setup script execution)
                return new Dictionary<int, List<int>>();
            }
            return result;
        }
    }
}
