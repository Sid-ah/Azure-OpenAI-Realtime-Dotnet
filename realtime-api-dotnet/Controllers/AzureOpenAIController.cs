using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
//using realtime_api_dotnet.Utilities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.VisualBasic;
using OpenAI.Chat;
using Microsoft.SemanticKernel;
using SqlDbSchemaExtractor.Schema;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using realtime_api_dotnet.Services;
using System.Text.Json.Serialization;

namespace AzureOpenAIDemo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AzureOpenAIController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly ILogger<AzureOpenAIController> _logger;
        //private readonly StatsLoader _statsLoader;
        private readonly DatabaseService _databaseService;
        private readonly AzureOpenAiService _azureOpenAiService;
        private readonly Kernel _kernel;
        private readonly string _dbSchema;
        private string _databaseConnectionString;
        private string _databaseDescription;
        private string _tables;

        public AzureOpenAIController(IConfiguration configuration, HttpClient httpClient, ILogger<AzureOpenAIController> logger, /*StatsLoader statsLoader, */DatabaseService databaseService, AzureOpenAiService azureOpenAiService)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _logger = logger;
            //_statsLoader = statsLoader;
            _databaseService = databaseService;
            _azureOpenAiService = azureOpenAiService;

            // database connection details
            _databaseConnectionString = _configuration["DatabaseConnection"];
            _databaseDescription = _configuration["DatabaseDescription"];
            _tables = _configuration["tables"];

            /*            
            // Initialize Semantic Kernel
            _kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    _configuration["AzureOpenAI:ChatDeploymentName"],
                    azureOpenAIClient
                )
                .Build();*/
        }

        [HttpPost("sessions")]
        public async Task<IActionResult> CreateSession([FromBody] SessionRequest request)
        {
            var resourceName = _configuration["AzureOpenAI:ResourceName"];
            var realtimeDeploymentName = _configuration["AzureOpenAI:RealtimeDeploymentName"];
            var apiKey = _configuration["AzureOpenAI:ApiKey"];
            var apiVersion = _configuration["AzureOpenAI:ApiVersion"];

            var sessionsUrl = $"https://{resourceName}.openai.azure.com/openai/realtimeapi/sessions?api-version={apiVersion}";

            // load the stats data so we can provide this as context for the agent to answer questions around this data only
            //await _statsLoader.LoadDataAsync();
            //string statsData = _statsLoader.FormatDataForLLMContext();

            //string systemPrompt = $"{request.SystemPrompt}\n\nHere is the player statistics data from the 2023-24 regular season that you can ues to answer questions:\n{statsData}";
            
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
                string currentUserQuery = request.Query;

                string conversationHistory = string.Join("\n", request.Messages.Select(m => $"{(m.Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "Assistant")}: {m.Text}"));

                string rewrittenQuery = await _azureOpenAiService.RewriteQuery(conversationHistory, request.Query);

                // set a retry of generating and executing the SQL query 3 times
                const int MaxRetries = 3;
                int attemptCount = 0;
                string generatedSqlQuery = null;
                string errorMessage = null;

                while (attemptCount < MaxRetries) {

                    try
                    {
                        generatedSqlQuery = await _azureOpenAiService.GenerateSqlQuery(rewrittenQuery, _databaseConnectionString, _databaseDescription, _tables, generatedSqlQuery, errorMessage);

                        var results = await _databaseService.ExecuteQueryAsync(generatedSqlQuery);

                        return Ok(results);
                    }
                    catch (Exception ex)
                    {
                        attemptCount++;

                        errorMessage = ex.Message;
                    }
                }

                return StatusCode(500, new { errorMessage = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing query");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        public class ChatMessageDto
        {
            public string Sender { get; set; }
            public string Text { get; set; }
        }

        public class QueryRequest
        {
            public string Query { get; set; }

            public List<ChatMessageDto> Messages { get; set; } = new List<ChatMessageDto>();
        }
    }
}