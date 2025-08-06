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

/// <summary>
/// Service orchestrating AI operations using Semantic Kernel plugins.
/// 
/// This service coordinates the complete NL2SQL pipeline:
/// 1. Intent Classification - Determines if query needs database lookup
/// 2. Query Rewriting - Enhances queries with conversation context  
/// 3. SQL Generation - Converts natural language to executable SQL
/// 4. Database Execution - Runs SQL and formats results
/// 
/// The service uses Semantic Kernel's plugin architecture for modularity,
/// allowing each AI capability to be independently developed, tested, and maintained.
/// </summary>
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
    /// Classifies a user query into "statistical" or "conversational" using Semantic Kernel.
    /// 
    /// This method uses the IntentClassificationPlugin to determine whether a user query
    /// requires database lookup (STATISTICAL) or can be handled as general conversation (CONVERSATIONAL).
    /// The classification considers full conversation history to properly handle follow-up questions.
    /// 
    /// Example:
    /// - "Who won the most races in 2023?" → STATISTICAL (needs database lookup)
    /// - "Hello, how are you?" → CONVERSATIONAL (general chat)
    /// - "How many points did he score?" → STATISTICAL (follow-up, needs context + database)
    /// </summary>
    /// <param name="conversationHistory">Full conversation history between user and agent</param>
    /// <param name="userQuery">Current user query to classify</param>
    /// <returns>true if this is a statistical query requiring database lookup, false otherwise</returns>
    public async Task<bool> ClassifyIntent(string conversationHistory, string userQuery)
    {
        try
        {
            // Invoke the Intent Classification plugin through Semantic Kernel
            // The plugin encapsulates the prompting logic and AI interaction
            var intentPlugin = _kernel.Plugins["IntentClassificationPlugin"];
            var result = await _kernel.InvokeAsync(intentPlugin["ClassifyIntentAsync"], new()
            {
                ["conversationHistory"] = conversationHistory,
                ["userQuery"] = userQuery
            });

            var responseText = result.ToString();

            // Parse the classification response from the AI
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
    /// Enhances user queries with conversation context using Semantic Kernel.
    /// 
    /// This method uses the QueryRewritePlugin to analyze conversation history and rewrite
    /// the current user query to include necessary context. This is crucial for follow-up
    /// questions that reference previous conversation elements.
    /// 
    /// Example transformations:
    /// - Original: "How many points did he score?"
    /// - Context: Previous discussion about Max Verstappen winning races in 2023
    /// - Rewritten: "How many points did Max Verstappen score in the 2023 Formula One season?"
    /// 
    /// This enhanced query enables accurate SQL generation where the original would fail.
    /// </summary>
    /// <param name="conversationHistory">Full conversation history between user and agent</param>
    /// <param name="userQuery">Current user query to enhance</param>
    /// <returns>Enhanced query with conversation context included</returns>
    public async Task<string> RewriteQuery(string conversationHistory, string userQuery)
    {
        try
        {
            // Invoke the Query Rewrite plugin through Semantic Kernel
            // This plugin analyzes conversation history to enhance the current query
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
    /// Generates executable SQL from natural language using Semantic Kernel.
    /// 
    /// This method uses the SqlGenerationPlugin to convert enhanced natural language queries
    /// into executable SQL statements. The plugin uses the database schema to ensure accurate
    /// table and column references, and includes retry logic for failed SQL attempts.
    /// 
    /// Features:
    /// - Schema-aware SQL generation using JSON schema from database introspection
    /// - Error recovery with retry logic (up to 3 attempts)
    /// - Single-line SQL output optimized for execution
    /// - Support for complex Formula One statistics queries
    /// 
    /// The generated SQL targets the F1 database tables:
    /// - F1Records (driver statistics, wins, points)
    /// - Constructor championships  
    /// - Circuit winners and records
    /// </summary>
    /// <param name="rewrittenQuery">Enhanced natural language query with full context</param>
    /// <param name="previousGeneratedSqlQuery">Optional: Previous SQL attempt (for retry scenarios)</param>
    /// <param name="sqlErrorMessage">Optional: Error message from previous SQL execution (for retry scenarios)</param>
    /// <returns>Executable SQL statement targeting the Formula One database</returns>
    public async Task<string> GenerateSqlQuery(string rewrittenQuery, string? previousGeneratedSqlQuery = null, string? sqlErrorMessage = null)
    {
        try
        {
            // Extract database schema for AI context
            // This provides the plugin with table structures, relationships, and data types
            var sqlHarness = new SqlSchemaProviderHarness(_databaseConnectionString, _nl2SqlConfig.Database.Description);
            var jsonSchema = await sqlHarness.ReverseEngineerSchemaJSONAsync(_nl2SqlConfig).ConfigureAwait(false);

            // Invoke the SQL Generation plugin through Semantic Kernel
            // The plugin uses schema + query + error context to generate accurate SQL
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