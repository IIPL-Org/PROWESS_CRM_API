using Microsoft.Data.SqlClient;

namespace Payroll_CRM_API.Data
{
    public interface IDbConnectionFactory
    {
        Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
        SqlConnection CreateConnection();
    }

    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
        }

        public async Task<SqlConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        public SqlConnection CreateConnection()
        {
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            return connection;
        }
    }
}
