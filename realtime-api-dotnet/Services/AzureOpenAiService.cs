using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using SqlDbSchemaExtractor;
using SqlDbSchemaExtractor.Schema;
using realtime_api_dotnet.Controllers;
using realtime_api_dotnet.Prompts;

namespace realtime_api_dotnet.Services;

public class AzureOpenAiService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAIController> _logger;
    private Nl2SqlConfigRoot _nl2SqlConfig;
    private string _databaseConnectionString;

    public AzureOpenAiService(IConfiguration configuration, ILogger<AzureOpenAIController> logger)
    {
        _logger = logger;
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
        _databaseConnectionString = configuration["DatabaseConnection"];

        _nl2SqlConfig = configuration.GetSection("Nl2SqlConfig").Get<Nl2SqlConfigRoot>();
    }

    /// <summary>
    /// Classifies a user query into "statistical" or "conversational".
    /// </summary>
    /// <param name="conversationHistory">Full conversation history between user and agent</param>
    /// <param name="userQuery">User query</param>
    /// <returns>true if this is a statistical query, false otherwise.</returns>
    public async Task<bool> ClassifyIntent(string conversationHistory, string userQuery)
    {
        // TODO: We are currently not rewriting the query, but it may be possible a follow up query which is about statitistics may be inteprepted as conversational
        // without taking into account the chat history context, so this may be needed
        var intentDetectionPrompt = CorePrompts.GetIntentClassificationPrompt(conversationHistory);

        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(intentDetectionPrompt),
                new UserChatMessage($"Classify this message: '{userQuery}'. Is this asking about NBA basketball statistics, players, teams, or scores (respond with STATISTICAL) or is it just a greeting or general conversation not related to data lookup (respond with CONVERSATIONAL)?")
            ]);

        var responseText = completion.Content[0].Text;

        // Parse the classification response
        bool isStatisticalQuery = responseText.Contains("STATISTICAL", StringComparison.OrdinalIgnoreCase);

        return isStatisticalQuery;
    }

    /// <summary>
    /// Uses the LLM to look at conversation history to rewrite, if necessary, the current user query. For example, if the user
    /// previously asked who the highest scoring player was in the 2023-24 NBA season and receives a players name then follows
    /// up and simply asks "How many steals did they have?", the rewritten query will be something similar to "How many steals
    /// did {players name} have during the 2023-24 season" which will allow for a proper SQL query to be generated whereas
    /// the original query would not.
    /// </summary>
    /// <param name="conversationHistory">Full conversation history between user and agent</param>
    /// <param name="userQuery">User query</param>
    /// <returns>The rewritten query to help the NL2SQL return valid results</returns>
    public async Task<string> RewriteQuery(string conversationHistory, string userQuery)
    {
        // For follow up questions, we want to ensure we have a query that represents any context. For example,
        // if the user asks "Who had the most steals on the season?" and the LLM answers "Marcus Smart". The
        // follow up may be "How many steals did he have?". Rather than generate a query for "How many steals did he have?",
        // we'll use the LLM to rewrite this query using history so it will be something like "How many steals did Marcus Smart have on the season?"
        var queryRewritePrompt = CorePrompts.GetQueryRewritePrompt(conversationHistory);

        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(queryRewritePrompt),
                new UserChatMessage($"User Prompt: '{userQuery}'")
            ]);

        string rewrittenQuery = completion.Content[0].Text;

        _logger.LogInformation($"Received query: {userQuery}");
        _logger.LogInformation($"Rewritten query: {rewrittenQuery}");

        return rewrittenQuery;
    }

    /// <summary>
    /// Generates a SQL query using the database schema and tables that will be targeted.
    /// </summary>
    /// <param name="rewrittenQuery">The enhanced, rewritten user query</param>
    /// <returns>A SQL statement that can be executed against the database</returns>
    public async Task<string> GenerateSqlQuery(string rewrittenQuery, string previousGeneratedSqlQuery = null, string sqlErrorMessage = null)
    {
        var sqlHarness = new SqlSchemaProviderHarness(_databaseConnectionString, _nl2SqlConfig.Database.Description);
        var jsonSchema = await sqlHarness.ReverseEngineerSchemaJSONAsync(_nl2SqlConfig).ConfigureAwait(false);

        var sqlPrompt = CorePrompts.GetSqlGenerationPrompt(jsonSchema, previousGeneratedSqlQuery, sqlErrorMessage);

        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(sqlPrompt),
                new UserChatMessage($"User Prompt: '{rewrittenQuery}'")
            ]);

        var generatedSqlStatement = completion.Content[0].Text;

        return generatedSqlStatement;
    }
}