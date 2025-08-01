using Azure.AI.OpenAI;
using Azure;
using OpenAI.Chat;
using OpenAI.Embeddings;
using SqlDbSchemaExtractor;
using SqlDbSchemaExtractor.Schema;
using realtime_api_dotnet.Controllers;
using realtime_api_dotnet.Prompts;
using Microsoft.Data.SqlClient;

namespace realtime_api_dotnet.Services;

public class AzureOpenAiService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<AzureOpenAIController> _logger;
    private Nl2SqlConfigRoot? _nl2SqlConfig;
    private string? _databaseConnectionString;
    private readonly EmbeddingClient? _embeddingClient;
    private readonly string? _embeddingDeploymentName;

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
        // Initialize Azure OpenAI Embeddings client (if configured)
        var embeddingModel = configuration["AzureOpenAI:EmbeddingDeploymentName"];
        if (!string.IsNullOrEmpty(embeddingModel))
        {
            _embeddingClient = azureOpenAIClient.GetEmbeddingClient(embeddingModel);
            _embeddingDeploymentName = embeddingModel;
        }
    }

    /// <summary>
    /// Classifies a user query into "statistical" or "conversational".
    /// </summary>
    /// <param name="conversationHistory">Full conversation history between user and agent</param>
    /// <param name="userQuery">User query</param>
    /// <returns>true if this is a statistical query, false otherwise.</returns>
    public async Task<bool> ClassifyIntent(string conversationHistory, string userQuery)
    {
        // Use conversation history to ensure follow-up questions are classified correctly
        var intentDetectionPrompt = CorePrompts.GetIntentClassificationPrompt(conversationHistory);

        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(intentDetectionPrompt),
                new UserChatMessage($"Classify this message: '{userQuery}'. Is this asking about Formula One racing statistics, drivers, teams, constructor championships, or race results (respond with STATISTICAL) or is it just a greeting or general conversation not related to data lookup (respond with CONVERSATIONAL)?")
            ]);

        var responseText = completion.Content[0].Text;

        // Parse the classification response
        bool isStatisticalQuery = responseText.Contains("STATISTICAL", StringComparison.OrdinalIgnoreCase);

        return isStatisticalQuery;
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
        // For follow up questions, we want to ensure we have a query that represents any context. For example,
        // if the user asks "Who won the most races this season?" and the LLM answers "Max Verstappen".
        // The follow up may be "How many points did he score?". Rather than generate a query for "How many points did he score?",
        // we'll use the LLM to rewrite this query using history so it will be something like "How many points did Max Verstappen score in the season?"
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
    public async Task<string> GenerateSqlQuery(string rewrittenQuery, string? previousGeneratedSqlQuery = null, string? sqlErrorMessage = null)
    {
        // var sqlHarness = new SqlSchemaProviderHarness(_databaseConnectionString, _nl2SqlConfig.Database.Description);
        // var jsonSchema = await sqlHarness.ReverseEngineerSchemaJSONAsync(_nl2SqlConfig).ConfigureAwait(false);

        // var sqlPrompt = CorePrompts.GetSqlGenerationPrompt(jsonSchema, previousGeneratedSqlQuery, sqlErrorMessage);

        var sqlHarness = new SqlSchemaProviderHarness(_databaseConnectionString, _nl2SqlConfig.Database.Description);
        // 1. Determine relevant tables via embedding gating
        string[] selectedTables = await GetRelevantTablesAsync(rewrittenQuery);
        // 2. Create a filtered schema config with only the selected tables
        var filteredConfig = new Nl2SqlConfigRoot
        {
            Database = new Nl2SqlConfig
            {
                Description = _nl2SqlConfig.Database.Description,
                Schemas = new List<DbSchema>()
            }
        };
        foreach (var schema in _nl2SqlConfig.Database.Schemas)
        {
            // Include the schema with filtered tables
            var filteredTables = schema.Tables.Where(t => selectedTables.Contains(t)).ToList();
            if (filteredTables.Any())
            {
                filteredConfig.Database.Schemas.Add(new DbSchema { Name = schema.Name, Tables = filteredTables });
            }
        }
        // 3. Reverse-engineer JSON schema for only the relevant tables
        var jsonSchema = await sqlHarness.ReverseEngineerSchemaJSONAsync(filteredConfig).ConfigureAwait(false);

        // 4. Generate the SQL prompt with the reduced schema context
        var sqlPrompt = CorePrompts.GetSqlGenerationPrompt(jsonSchema, previousGeneratedSqlQuery, sqlErrorMessage);


        ChatCompletion completion = await _chatClient.CompleteChatAsync(
            [
                new SystemChatMessage(sqlPrompt),
                new UserChatMessage($"User Prompt: '{rewrittenQuery}'")
            ]);

        var generatedSqlStatement = completion.Content[0].Text;

        return generatedSqlStatement;
    }

    /// <summary>
    /// Uses embeddings to find which tables are most relevant to the query.
    /// </summary>
    private async Task<string[]> GetRelevantTablesAsync(string userQuery)
    {
        if (_embeddingClient == null || string.IsNullOrEmpty(_embeddingDeploymentName))
            return _nl2SqlConfig.Database.Schemas.SelectMany(s => s.Tables).ToArray(); 
            // If no embedding model configured, fall back to all tables

        // Compute embedding for the user query
        var queryEmbeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(userQuery);
        var queryEmbedding = queryEmbeddingResponse.Value.ToFloats();

        // Prepare list of table representations (e.g., "Schema.Table: Column1, Column2, ...")
        var tableList = new List<(string TableName, string SchemaName, float[] Embedding)>();
        using var connection = new SqlConnection(_databaseConnectionString);
        await connection.OpenAsync();
        foreach (var schema in _nl2SqlConfig.Database.Schemas)
        {
            foreach (var tableName in schema.Tables)
            {
                // Create a text description of the table (name plus column names)
                string tableDescText = $"{schema.Name}.{tableName}";
                // Optionally include column names to capture semantic context
                using var cmd = new SqlCommand(
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table",
                    connection);
                cmd.Parameters.AddWithValue("@schema", schema.Name);
                cmd.Parameters.AddWithValue("@table", tableName);
                using var reader = await cmd.ExecuteReaderAsync();
                var columnNames = new List<string>();
                while (await reader.ReadAsync())
                {
                    columnNames.Add(reader.GetString(0));
                }
                reader.Close();
                if (columnNames.Count > 0)
                {
                    tableDescText += $": {string.Join(", ", columnNames)}";
                }

                // Compute embedding for the table description text
                var tableEmbeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(tableDescText);
                var tableEmbedding = tableEmbeddingResponse.Value.ToFloats();
                tableList.Add((tableName, schema.Name, tableEmbedding.ToArray()));
            }
        }
        await connection.CloseAsync();

        // Calculate cosine similarity between query and each table
        float[] queryVector = queryEmbedding.ToArray();
        var tableSimilarities = tableList.Select(t =>
        {
            float[] tableVector = t.Embedding;
            // Compute cosine similarity
            float dot = 0, normQ = 0, normT = 0;
            for (int i = 0; i < queryVector.Length; i++)
            {
                dot += queryVector[i] * tableVector[i];
                normQ += queryVector[i] * queryVector[i];
                normT += tableVector[i] * tableVector[i];
            }
            float cosineSim = dot / ((float)Math.Sqrt(normQ) * (float)Math.Sqrt(normT));
            return (Table: $"{t.SchemaName}.{t.TableName}", Similarity: cosineSim);
        }).OrderByDescending(x => x.Similarity).ToList();

        // Select the top tables with highest similarity (e.g., top 1–2)
        double threshold = 0.20; // optional: include any table with similarity above 0.2
        var selectedTables = tableSimilarities
                                .Where(x => x.Similarity >= threshold || x == tableSimilarities.First())
                                .Take(2)  // limit to top 2 to keep prompt concise
                                .Select(x => x.Table.Split('.')[1])  // return table names only
                                .ToArray();
        _logger.LogInformation($"Embedding-gated tables for query '{userQuery}': {string.Join(", ", selectedTables)}");
        return selectedTables;
    }
}