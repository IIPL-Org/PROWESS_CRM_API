using Microsoft.Data.SqlClient;
using PROWESS_CRM.Models;

namespace PROWESS_CRM.Data
{
    public interface IProwessModuleRepository
    {
        Task<IEnumerable<ProwessModuleMaster>> GetAllProwessModulesAsync(CancellationToken cancellationToken = default);
    }

    public class ProwessModuleRepository : IProwessModuleRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ProwessModuleRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<IEnumerable<ProwessModuleMaster>> GetAllProwessModulesAsync(CancellationToken cancellationToken = default)
        {
            var modules = new List<ProwessModuleMaster>();
            const string sql = @"
                SELECT 
                    Module_id, Module_name, Serial_code, created_date, isActive, Flag, pathname, icons_name, Module_Prefix
                FROM dbo.Prowess_Module_Master
                WHERE UPPER(ISNULL(isActive, 'Y')) = 'Y'
                ORDER BY ISNULL(Serial_code, Module_id) ASC, Module_id ASC;";

            try
            {
                using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                using var command = new SqlCommand(sql, connection);
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                int idOrd = reader.GetOrdinal("Module_id");
                int nameOrd = reader.GetOrdinal("Module_name");
                int serialOrd = reader.GetOrdinal("Serial_code");
                int createdOrd = reader.GetOrdinal("created_date");
                int activeOrd = reader.GetOrdinal("isActive");
                int flagOrd = reader.GetOrdinal("Flag");
                int pathOrd = reader.GetOrdinal("pathname");
                int iconOrd = reader.GetOrdinal("icons_name");
                int prefixOrd = reader.GetOrdinal("Module_Prefix");

                while (await reader.ReadAsync(cancellationToken))
                {
                    string? prefix = reader.IsDBNull(prefixOrd) ? null : reader.GetString(prefixOrd).Trim();
                    string? pathname = reader.IsDBNull(pathOrd) ? null : reader.GetString(pathOrd).Trim();
                    string? activeStr = reader.IsDBNull(activeOrd) ? "Y" : reader.GetString(activeOrd).Trim();
                    string? flagStr = reader.IsDBNull(flagOrd) ? null : reader.GetString(flagOrd).Trim();

                    modules.Add(new ProwessModuleMaster
                    {
                        ModuleId = reader.GetInt32(idOrd),
                        ModuleName = reader.IsDBNull(nameOrd) ? string.Empty : reader.GetString(nameOrd).Trim(),
                        SerialCode = reader.IsDBNull(serialOrd) ? null : reader.GetInt32(serialOrd),
                        CreatedDate = reader.IsDBNull(createdOrd) ? null : reader.GetDateTime(createdOrd),
                        IsActive = string.IsNullOrEmpty(activeStr) ? 'Y' : char.ToUpper(activeStr[0]),
                        Flag = string.IsNullOrEmpty(flagStr) ? null : flagStr[0],
                        Pathname = pathname,
                        IconsName = reader.IsDBNull(iconOrd) ? null : reader.GetString(iconOrd).Trim(),
                        ModulePrefix = prefix,
                        ShortName = prefix ?? string.Empty,
                        Description = pathname
                    });
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Table Prowess_Module_Master does not exist yet
                return new List<ProwessModuleMaster>();
            }

            return modules;
        }
    }
}
