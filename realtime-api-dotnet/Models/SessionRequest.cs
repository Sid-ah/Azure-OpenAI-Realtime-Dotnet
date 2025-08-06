using realtime_api_dotnet.Prompts;

public class SessionRequest
{
    public required string Voice { get; set; }
    public string SystemPrompt { get; set; } = CorePrompts.GetSystemPrompt();
}
