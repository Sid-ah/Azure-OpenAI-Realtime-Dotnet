namespace realtime_api_dotnet.Services
{
    using Azure.AI.OpenAI;
    using Azure;
    using OpenAI.Chat;
    using AzureOpenAIDemo.Api.Controllers;
    using Azure.Core;
    using SqlDbSchemaExtractor.Schema;

    public class AzureOpenAiService
    {
        private readonly ChatClient _chatClient;
        private readonly ILogger<AzureOpenAIController> _logger;

        public AzureOpenAiService(IConfiguration configuration, ILogger<AzureOpenAIController> logger)
        {
            _logger = logger;
            var resourceName = configuration["AzureOpenAI:ResourceName"];

            // Initialize the Azure OpenAI client
            string endpoint = $"https://{resourceName}.openai.azure.com";
            string key = configuration["AzureOpenAI:ApiKey"];

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
            string intentDetectionPrompt = $@"You are an AI assistant that determines if a user's question requires NBA basketball statistics or is just a conversational message.
                                                The user's question may be a short follow up question so you must uset he context of the chat history to determine if the user's question
                                                is related to statistics.
                                                You must only respond with 'STATISTICAL' or 'CONVERSATIONAL'

                                                Chat History: {conversationHistory}
                                                ";

            ChatCompletion completion = await _chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(intentDetectionPrompt),
                    new UserChatMessage($"Classify this message: '{userQuery}'. Is this asking about NBA basketball statistics, players, teams, or scores (respond with STATISTICAL) or is it just a greeting or general conversation not related to data lookup (respond with CONVERSATIONAL)?")
                ]);

            string responseText = completion.Content[0].Text;

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
            string queryRewritePrompt = $@"You are a query enhancer that rewrites the latest user question to based on contextual information from
                                                previous exchanges in the chat history, if necessary. If the question seems to be a follow-up question, write it so the full context is
                                                preserved. If the question is already explicit, return it unchanged. Only return the rewritten
                                                question text without explanations.

                                                Chat History: {conversationHistory}
                                            ";


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
        /// <param name="databaseConnectionString">SQL DB connection string</param>
        /// <param name="databaseDescription">Description of the database</param>
        /// <param name="tables">Pipe separated list of table names to be targeted with the SQL query</param>
        /// <returns>A SQL statement that can be executed against the database</returns>
        public async Task<string> GenerateSqlQuery(string rewrittenQuery, string databaseConnectionString, string databaseDescription, string tables, string previousGeneratedSqlQuery = null, string sqlErrorMessage = null)
        {
            var sqlHarness = new SqlSchemaProviderHarness(databaseConnectionString, databaseDescription);
            var tableNames = tables.Split("|");
            var jsonSchema = await sqlHarness.ReverseEngineerSchemaJSONAsync(tableNames);

            string sqlPrompt = $@"You are responsible for generating a SQL query in response to user input. Only target the tables described in the given database schema.

                                    Perform each of the following steps:
                                    1. Generate a query that is always entirely based on the targeted database schema.
                                    2. Return ONLY the SQL query, nothing more.

                                    IMPORTANT:
                                        - Return only a valid SQL query.
                                          - Do not include any backticks, newlines, backslashes, escape sequences, or any other formatting.
                                          - The entire SQL query must appear on a single line, with no whitespace except single spaces after colons and commas.
                                          - Return only the SQL query and nothing else.

                                    The database schema is described according to the following json schema:
                                    {jsonSchema}";

            // if we are fixing a failed query, add the error context so the LLM can try to regenerate a proper
            if (!string.IsNullOrEmpty(previousGeneratedSqlQuery) && !string.IsNullOrEmpty(sqlErrorMessage))
            {
                sqlPrompt += $@"
                                IMPORTANT - FIX FAILED QUERY:
                                The following SQL query failed with this error: {sqlErrorMessage}

                                Failed SQL query: {previousGeneratedSqlQuery}

                                Please fix this SQL query to address this specific error. Make sure your fixed query follows all of the formatting instructions above.";
            }

            ChatCompletion completion = await _chatClient.CompleteChatAsync(
                [
                    new SystemChatMessage(sqlPrompt),
                    new UserChatMessage($"User Prompt: '{rewrittenQuery}'")
                ]);

            string generatedSqlStatement = completion.Content[0].Text;

            return generatedSqlStatement;
        }
    }
}