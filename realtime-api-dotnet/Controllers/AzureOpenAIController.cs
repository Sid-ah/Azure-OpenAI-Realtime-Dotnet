using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using realtime_api_dotnet.Services;

namespace realtime_api_dotnet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AzureOpenAIController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureOpenAIController> _logger;
    private readonly DatabaseService _databaseService;
    private readonly AzureOpenAiService _azureOpenAiService;
    //private readonly Kernel _kernel;

    public AzureOpenAIController(
        IConfiguration configuration,
        HttpClient httpClient, 
        ILogger<AzureOpenAIController> logger, 
        DatabaseService databaseService,
        AzureOpenAiService azureOpenAiService)
    {
        _configuration = configuration;
        _httpClient = httpClient;
        _logger = logger;
        _databaseService = databaseService;
        _azureOpenAiService = azureOpenAiService;            
    }

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] SessionRequest request)
    {
        var resourceName = _configuration["AzureOpenAI:ResourceName"];
        var realtimeDeploymentName = _configuration["AzureOpenAI:RealtimeDeploymentName"];
        var apiKey = _configuration["AzureOpenAI:ApiKey"];
        var apiVersion = _configuration["AzureOpenAI:ApiVersion"];

        var sessionsUrl = $"https://{resourceName}.openai.azure.com/openai/realtimeapi/sessions?api-version={apiVersion}";

        var body = new
        {
            model = realtimeDeploymentName,
            voice = request.Voice,
            instructions = request.SystemPrompt
        };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
        
        var response = await _httpClient.PostAsync(sessionsUrl, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to create session: {response.StatusCode}, {error}");
            return StatusCode((int)response.StatusCode, error);
        }

        var sessionResponse = await response.Content.ReadAsStringAsync();

        var jsonResponse = JsonDocument.Parse(sessionResponse).RootElement;

        // include the system prompt in the response so the front end receives it
        var enhancedResponse = new
        {
            id = jsonResponse.GetProperty("id").GetString(),
            client_secret = jsonResponse.GetProperty("client_secret"),
            system_prompt = request.SystemPrompt
        };

        return Ok(JsonSerializer.Serialize(enhancedResponse));
    }

    [HttpPost("rtc")]
    public async Task<IActionResult> ConnectRTC([FromBody] RTCRequest request)
    {
        var rtcUrl = $"https://{request.Region}.realtimeapi-preview.ai.azure.com/v1/realtimertc?model={request.DeploymentName}";

        // Create HttpContent with application/sdp without charset parameter
        var content = new StringContent(request.Sdp);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/sdp");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {request.EphemeralKey}");

        var response = await _httpClient.PostAsync(rtcUrl, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError($"RTC connect failed: {response.StatusCode}, {error}");
            return StatusCode((int)response.StatusCode, error);
        }

        var answerSdp = await response.Content.ReadAsStringAsync();
        return Content(answerSdp, "application/sdp");
    }

    /// <summary>
    /// Classifies the intent of the user prompt into "statistical" or "conversational" so the LLM can handle the request appropriately.
    /// If the intent is "statistical", the front end will then call the query endpoint so the NL2SQL is used to answer the question based
    /// on the data in the database. If the intent is "conversational", the LLM is free to respond on its own.
    /// </summary>
    /// <param name="request">QueryRequest object, including chat history.</param>
    /// <returns></returns>
    [HttpPost("classify-intent")]
    public async Task<IActionResult> ClassifyIntent([FromBody] QueryRequest request)
    {
        try
        {
            string conversationHistory = string.Join("\n", request.Messages.Select(m => $"{(m.Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant")}: {m.Text}"));

            _logger.LogInformation("Processing intent classification for: {Query}", request.Query);

            if (string.IsNullOrEmpty(request.Query))
            {
                return BadRequest(new { error = "Query cannot be empty" });
            }

            bool isStatisticalQuery = await _azureOpenAiService.ClassifyIntent(conversationHistory, request.Query);

            return Ok(isStatisticalQuery);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during intent classification");
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Classifies the intent of the user prompt into "statistical" or "conversational" so the LLM can handle the request appropriately.
    /// If the intent is "statistical", the front end will then call the query endpoint so the NL2SQL is used to answer the question based
    /// on the data in the database. If the intent is "conversational", the LLM is free to respond on its own.
    /// </summary>
    /// <param name="request">QueryRequest object, including chat history.</param>
    /// <returns></returns>
    [HttpPost("query")]
    public async Task<IActionResult> ExecuteNaturalLanguageQuery([FromBody] QueryRequest request)
    {
        try
        {
            var currentUserQuery = request.Query;

            var conversationHistory = string.Join("\n", request.Messages.Select(m => $"{(m.Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant")}: {m.Text}"));

            var rewrittenQuery = await _azureOpenAiService.RewriteQuery(conversationHistory, request.Query);

            // set a retry of generating and executing the SQL query 3 times
            const int MaxRetries = 3;
            int attemptCount = 0;
            var generatedSqlQuery = string.Empty;
            var errorMessage = string.Empty;

            while (attemptCount < MaxRetries) {

                try
                {
                    generatedSqlQuery = await _azureOpenAiService.GenerateSqlQuery(rewrittenQuery, generatedSqlQuery, errorMessage);

                    var results = await _databaseService.ExecuteQueryAsync(generatedSqlQuery);

                    return Ok(results);
                }
                catch (Exception ex)
                {
                    attemptCount++;

                    errorMessage = ex.Message;
                }
            }

            return StatusCode(500, new { errorMessage });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    public class ChatMessageDto
    {
        public required string Sender { get; set; }
        public required string Text { get; set; }
    }

    public class QueryRequest
    {
        public required string Query { get; set; }

        public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
    }
}