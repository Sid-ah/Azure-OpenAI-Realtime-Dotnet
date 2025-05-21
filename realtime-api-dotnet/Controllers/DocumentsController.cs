// Controllers/DocumentsController.cs
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using AzureOpenAIDemo.Api.Models;
using AzureOpenAIDemo.Api.Services;

namespace AzureOpenAIDemo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly ICognitiveSearchService _cognitiveSearchService;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(ICognitiveSearchService cognitiveSearchService, ILogger<DocumentsController> logger)
        {
            _cognitiveSearchService = cognitiveSearchService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> UploadDocument([FromBody] DocumentUploadRequest request)
        {
            try
            {
                var document = new DocumentModel
                {
                    Title = request.Title,
                    Content = request.Content
                };

                var result = await _cognitiveSearchService.IndexDocumentAsync(document);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading document");
                return StatusCode(500, $"Error uploading document: {ex.Message}");
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            try
            {
                var documents = await _cognitiveSearchService.GetAllDocumentsAsync();
                return Ok(documents);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents");
                return StatusCode(500, $"Error retrieving documents: {ex.Message}");
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchDocuments([FromQuery] string query, [FromQuery] int topK = 3)
        {
            try
            {
                var results = await _cognitiveSearchService.SearchDocumentsAsync(query, topK);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents");
                return StatusCode(500, $"Error searching documents: {ex.Message}");
            }
        }
    }

    public class DocumentUploadRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}