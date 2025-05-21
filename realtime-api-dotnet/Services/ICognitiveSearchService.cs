// Services/ICognitiveSearchService.cs
using AzureOpenAIDemo.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AzureOpenAIDemo.Api.Services
{
    public interface ICognitiveSearchService
    {
        Task<DocumentModel> IndexDocumentAsync(DocumentModel document);
        Task<IEnumerable<DocumentModel>> GetAllDocumentsAsync();
        Task<IEnumerable<DocumentModel>> SearchDocumentsAsync(string query, int topK = 3);
        Task<string> GenerateRAGContextAsync(string query, int topK = 3, float threshold = 0.7f);
    }
}