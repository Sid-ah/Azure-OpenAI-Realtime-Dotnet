using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace realtime_api_dotnet.Plugins;

/// <summary>
/// Plugin for generating SQL queries from natural language
/// </summary>
public class SqlGenerationPlugin
{
    [KernelFunction, Description("Generates a SQL query from natural language based on database schema")]
    public async Task<string> GenerateSqlQueryAsync(
        [Description("The natural language query to convert to SQL")] string naturalLanguageQuery,
        [Description("The database schema in JSON format")] string jsonSchema,
        [Description("Optional: previous SQL query that failed")] string? previousSqlQuery,
        [Description("Optional: error message from previous SQL execution")] string? sqlErrorMessage,
        Kernel kernel)
    {
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
        if (!string.IsNullOrEmpty(previousSqlQuery) && !string.IsNullOrEmpty(sqlErrorMessage))
        {
            sqlPrompt += $@"
                            IMPORTANT - FIX FAILED QUERY:
                            The following SQL query failed with this error: {sqlErrorMessage}

                            Failed SQL query: {previousSqlQuery}

                            Please fix this SQL query to address this specific error. Make sure your fixed query follows all of the formatting instructions above.";
        }

        sqlPrompt += $"\n\nUser Prompt: '{naturalLanguageQuery}'";

        var result = await kernel.InvokePromptAsync(sqlPrompt);
        return result.ToString();
    }
}
