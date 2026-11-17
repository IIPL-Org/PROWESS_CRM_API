using System.Data;
using Microsoft.Data.SqlClient;
using Payroll_CRM_API.Models;

namespace Payroll_CRM_API.Data
{
    public interface IModuleRepository
    {
        Task<IEnumerable<ModuleMaster>> GetAllModulesAsync(CancellationToken cancellationToken = default);
    }

    public class ModuleRepository : IModuleRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ModuleRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<ModuleMaster>> GetAllModulesAsync(CancellationToken cancellationToken = default)
        {
            var modules = new List<ModuleMaster>();
            const string sql = @"
                SELECT 
                    Module_Id, Module_Name, Short_Name, Description, Is_Active
                FROM Module_Master
                WHERE UPPER(Is_Active) = 'Y'
                ORDER BY Module_Id ASC;";

            using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            using var command = new SqlCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                modules.Add(new ModuleMaster
                {
                    ModuleId = reader.GetInt32(reader.GetOrdinal("Module_Id")),
                    ModuleName = reader.GetString(reader.GetOrdinal("Module_Name")),
                    ShortName = reader.GetString(reader.GetOrdinal("Short_Name")),
                    Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
                    IsActive = reader.GetString(reader.GetOrdinal("Is_Active"))[0]
                });
            }

            return modules;
        }
    }
}
