using AzureOpenAIDemo.Api.Prompts;

public class SessionRequest
{
    public string Voice { get; set; }
    public string SystemPrompt { get; set; } = CorePrompts.SystemPrompt;
}
