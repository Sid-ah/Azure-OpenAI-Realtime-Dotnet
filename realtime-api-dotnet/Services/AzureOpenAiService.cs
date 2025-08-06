using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using SqlDbSchemaExtractor;
using SqlDbSchemaExtractor.Schema;
using realtime_api_dotnet.Controllers;
using realtime_api_dotnet.Prompts;
using Microsoft.SemanticKernel;
using realtime_api_dotnet.Plugins;

namespace realtime_api_dotnet.Services;

public class AzureOpenAiService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAIController> _logger;
    private readonly Kernel _kernel;
    private readonly Nl2SqlConfigRoot _nl2SqlConfig;
    private readonly string _databaseConnectionString;

    public AzureOpenAiService(IConfiguration configuration, ILogger<AzureOpenAIController> logger, Kernel kernel)
    {
        _logger = logger;
        _kernel = kernel;
        var resourceName = configuration["AzureOpenAI:ResourceName"];

        // Initialize the Azure OpenAI client
        var endpoint = $"https://{resourceName}.openai.azure.com";
        var key = configuration["AzureOpenAI:ApiKey"];

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            _logger.LogError("Azure OpenAI configuration missing. Check your app settings.");
            throw new InvalidOperationException("Azure OpenAI configuration is incomplete");
        }

        AzureOpenAIClient azureOpenAIClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(key)
        );

        var chatDeploymentName = configuration["AzureOpenAI:ChatDeploymentName"];
        _chatClient = azureOpenAIClient.GetChatClient(chatDeploymentName);            
        
        // database connection details
        _databaseConnectionString = configuration["DatabaseConnection"] ?? throw new InvalidOperationException("DatabaseConnection configuration is missing");

        _nl2SqlConfig = configuration.GetSection("Nl2SqlConfig").Get<Nl2SqlConfigRoot>() ?? throw new InvalidOperationException("Nl2SqlConfig configuration is missing");
    }

    /// <summary>
    /// Classifies a user query into "statistical" or "conversational".
    /// </summary>
    /// <param name="conversationHistory">Full conversation history between user and agent</param>
    /// <param name="userQuery">User query</param>
    /// <returns>true if this is a statistical query, false otherwise.</returns>
    public async Task<bool> ClassifyIntent(string conversationHistory, string userQuery)
    {
        try
        {
            var intentPlugin = _kernel.Plugins["IntentClassificationPlugin"];
            var result = await _kernel.InvokeAsync(intentPlugin["ClassifyIntentAsync"], new()
            {
                ["conversationHistory"] = conversationHistory,
                ["userQuery"] = userQuery
            });

            var responseText = result.ToString();

            // Parse the classification response
            bool isStatisticalQuery = responseText.Contains("STATISTICAL", StringComparison.OrdinalIgnoreCase);

            return isStatisticalQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in intent classification using Semantic Kernel");
            throw;
        }
    }

    /// <summary>
    /// Uses the LLM to look at conversation history to rewrite, if necessary, the current user query. For example, if the user
    /// previously asked who won the most races in a recent Formula One season and receives a driver's name then follows
    /// up and simply asks "How many points did they score?", the rewritten query will be something similar to "How many points
    /// did {driver name} score during that season" which will allow for a proper SQL query to be generated whereas
    /// the original query would not.
    /// </summary>
    /// <param name="conversationHistory">Full conversation history between user and agent</param>
    /// <param name="userQuery">User query</param>
    /// <returns>The rewritten query to help the NL2SQL return valid results</returns>
    public async Task<string> RewriteQuery(string conversationHistory, string userQuery)
    {
        try
        {
            var queryRewritePlugin = _kernel.Plugins["QueryRewritePlugin"];
            var result = await _kernel.InvokeAsync(queryRewritePlugin["RewriteQueryAsync"], new()
            {
                ["conversationHistory"] = conversationHistory,
                ["userQuery"] = userQuery
            });

            string rewrittenQuery = result.ToString();

            _logger.LogInformation($"Received query: {userQuery}");
            _logger.LogInformation($"Rewritten query: {rewrittenQuery}");

            return rewrittenQuery;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in query rewriting using Semantic Kernel");
            throw;
        }
    }

    /// <summary>
    /// Generates a SQL query using the database schema and tables that will be targeted.
    /// </summary>
    /// <param name="rewrittenQuery">The enhanced, rewritten user query</param>
    /// <returns>A SQL statement that can be executed against the database</returns>
    public async Task<string> GenerateSqlQuery(string rewrittenQuery, string? previousGeneratedSqlQuery = null, string? sqlErrorMessage = null)
    {
        try
        {
            var sqlHarness = new SqlSchemaProviderHarness(_databaseConnectionString, _nl2SqlConfig.Database.Description);
            var jsonSchema = await sqlHarness.ReverseEngineerSchemaJSONAsync(_nl2SqlConfig).ConfigureAwait(false);

            var sqlGenerationPlugin = _kernel.Plugins["SqlGenerationPlugin"];
            var result = await _kernel.InvokeAsync(sqlGenerationPlugin["GenerateSqlQueryAsync"], new()
            {
                ["naturalLanguageQuery"] = rewrittenQuery,
                ["jsonSchema"] = jsonSchema,
                ["previousSqlQuery"] = previousGeneratedSqlQuery,
                ["sqlErrorMessage"] = sqlErrorMessage
            });

            var generatedSqlStatement = result.ToString();

            return generatedSqlStatement;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SQL generation using Semantic Kernel");
            throw;
        }
    }
}