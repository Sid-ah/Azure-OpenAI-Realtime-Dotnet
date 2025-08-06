using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace realtime_api_dotnet.Plugins;

/// <summary>
/// Plugin for classifying user intent as statistical or conversational
/// </summary>
public class IntentClassificationPlugin
{
    [KernelFunction, Description("Classifies a user query as either statistical (requiring database lookup) or conversational (general chat)")]
    public async Task<string> ClassifyIntentAsync(
        [Description("The full conversation history between user and assistant")] string conversationHistory,
        [Description("The current user query to classify")] string userQuery,
        Kernel kernel)
    {
        var prompt = $@"You are an AI assistant that determines if a user's question requires Formula One racing statistics or is just a conversational message.
                        The user's question may be a short follow up question so you must use the context of the chat history to determine if the user's question
                        is related to statistics.
                        You must only respond with 'STATISTICAL' or 'CONVERSATIONAL'

                        Chat History: {conversationHistory}
                        
                        Classify this message: '{userQuery}'. Is this asking about Formula One racing statistics, drivers, teams, constructor championships, or race results (respond with STATISTICAL) or is it just a greeting or general conversation not related to data lookup (respond with CONVERSATIONAL)?";

        var result = await kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }
}
