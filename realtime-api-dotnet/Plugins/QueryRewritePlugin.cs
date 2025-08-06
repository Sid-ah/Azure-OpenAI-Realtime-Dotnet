using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace realtime_api_dotnet.Plugins;

/// <summary>
/// Plugin for rewriting user queries with context from conversation history
/// </summary>
public class QueryRewritePlugin
{
    [KernelFunction, Description("Rewrites a user query to include context from conversation history for better database lookup")]
    public async Task<string> RewriteQueryAsync(
        [Description("The full conversation history between user and assistant")] string conversationHistory,
        [Description("The current user query to rewrite")] string userQuery,
        Kernel kernel)
    {
        var prompt = $@"You are a query enhancer that rewrites the latest user question to based on contextual information from
                        previous exchanges in the chat history, if necessary. If the question seems to be a follow-up question, write it so the full context is
                        preserved. If the question is already explicit, return it unchanged. Only return the rewritten
                        question text without explanations.

                        Chat History: {conversationHistory}
                        
                        User Prompt: '{userQuery}'";

        var result = await kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }
}
