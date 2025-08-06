using Microsoft.Data.SqlClient;
namespace realtime_api_dotnet.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration configuration)
    {
        _connectionString = configuration["DatabaseConnection"] ?? throw new ArgumentException("Database connection string is missing or empty. Please check your configuration.");
    }

    public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sqlQuery)
    {
        var results = new List<Dictionary<string, object>>();

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            using var command = new SqlCommand(sqlQuery, connection);

            // Set a timeout to prevent long-running queries
            command.CommandTimeout = 30;

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? (object?)null : reader.GetValue(i);
                }

                results.Add(row);
            }
        }

        return results;
    }
}
