using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using realtime_api_dotnet.Services;

namespace realtime_api_dotnet.Plugins;

/// <summary>
/// Plugin for executing database queries
/// </summary>
public class DatabasePlugin
{
    private readonly DatabaseService _databaseService;

    public DatabasePlugin(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    [KernelFunction, Description("Executes a SQL query against the Formula One database and returns the results")]
    public async Task<string> ExecuteQueryAsync(
        [Description("The SQL query to execute")] string sqlQuery)
    {
        try
        {
            var results = await _databaseService.ExecuteQueryAsync(sqlQuery);
            
            // Convert results to JSON string for the LLM to process
            var jsonResults = JsonSerializer.Serialize(results, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            return jsonResults;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Database query failed: {ex.Message}", ex);
        }
    }
}
