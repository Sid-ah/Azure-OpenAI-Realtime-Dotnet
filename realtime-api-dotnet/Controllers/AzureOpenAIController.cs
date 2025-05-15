// Controllers/AzureOpenAIController.cs
using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureOpenAIDemo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AzureOpenAIController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureOpenAIController> _logger;

        public AzureOpenAIController(IConfiguration configuration, HttpClient httpClient, ILogger<AzureOpenAIController> logger)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
        }

        [HttpPost("sessions")]
        public async Task<IActionResult> CreateSession([FromBody] SessionRequest request)
        {
            var resourceName = _configuration["AzureOpenAI:ResourceName"];
            var deploymentName = _configuration["AzureOpenAI:DeploymentName"];
            var apiKey = _configuration["AzureOpenAI:ApiKey"];
            var apiVersion = _configuration["AzureOpenAI:ApiVersion"];

            var sessionsUrl = $"https://{resourceName}.openai.azure.com/openai/realtimeapi/sessions?api-version={apiVersion}";
            
            var body = new
            {
                model = deploymentName,
                voice = request.Voice
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
            return Ok(sessionResponse);
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
    }
}