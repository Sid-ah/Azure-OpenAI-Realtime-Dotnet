using realtime_api_dotnet.Prompts;

public class SessionRequest
{
    public string Voice { get; set; }
    public string SystemPrompt { get; set; } = CorePrompts.GetSystemmPrompt();
}
