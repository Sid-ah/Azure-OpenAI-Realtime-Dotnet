// Services/CognitiveSearchService.cs
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using AzureOpenAIDemo.Api.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.AI.OpenAI;

namespace AzureOpenAIDemo.Api.Services
{
    public class CognitiveSearchService : ICognitiveSearchService
    {
        private readonly SearchIndexClient _searchIndexClient;
        private readonly SearchClient _searchClient;
        private readonly OpenAIClient _openAIClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CognitiveSearchService> _logger;
        private readonly string _indexName;
        private readonly string _embeddingDeployment;

        public CognitiveSearchService(IConfiguration configuration, ILogger<CognitiveSearchService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            var searchServiceEndpoint = _configuration["AzureCognitiveSearch:Endpoint"];
            var searchServiceApiKey = _configuration["AzureCognitiveSearch:ApiKey"];
            _indexName = _configuration["AzureCognitiveSearch:IndexName"] ?? "documents";
            
            var openAIEndpoint = _configuration["AzureOpenAI:Endpoint"];
            var openAIKey = _configuration["AzureOpenAI:ApiKey"];
            _embeddingDeployment = _configuration["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-ada-002";

            _searchIndexClient = new SearchIndexClient(
                new Uri(searchServiceEndpoint),
                new AzureKeyCredential(searchServiceApiKey));
            
            _searchClient = new SearchClient(
                new Uri(searchServiceEndpoint),
                _indexName,
                new AzureKeyCredential(searchServiceApiKey));
            
            _openAIClient = new OpenAIClient(
                new Uri(openAIEndpoint),
                new AzureKeyCredential(openAIKey));

            // Create index if it doesn't exist
            CreateIndexIfNotExistsAsync().Wait();
        }

        private async Task CreateIndexIfNotExistsAsync()
        {
            try
            {
                // Create the search index with vector search capabilities
                var indexExists = await _searchIndexClient.GetIndexAsync(_indexName) is not null;
                if (!indexExists)
                {
                    var vectorConfig = new VectorSearchConfiguration("myHnswConfig", 
                        new HnswParameters(metric: VectorSearchAlgorithmMetric.Cosine));

                    var fieldBuilder = new FieldBuilder();
                    var searchFields = fieldBuilder.Build(typeof(DocumentModel));

                    var vectorSearchField = new SearchField("Embedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsKey = false,
                        IsFilterable = false,
                        IsSearchable = false,
                        VectorSearchDimensions = 1536,
                        VectorSearchConfiguration = "myHnswConfig"
                    };

                    var contentField = new SearchField("Content", SearchFieldDataType.String)
                    {
                        IsKey = false,
                        IsFilterable = false,
                        IsSearchable = true
                    };

                    var titleField = new SearchField("Title", SearchFieldDataType.String)
                    {
                        IsKey = false,
                        IsFilterable = true,
                        IsSearchable = true
                    };

                    var idField = new SearchField("Id", SearchFieldDataType.String)
                    {
                        IsKey = true,
                        IsFilterable = true,
                        IsSearchable = false
                    };

                    var fields = new List<SearchField> {
                        idField,
                        contentField,
                        titleField,
                        vectorSearchField
                    };

                    var definition = new SearchIndex(_indexName, fields)
                    {
                        VectorSearch = new VectorSearch(new[] { vectorConfig })
                    };

                    await _searchIndexClient.CreateIndexAsync(definition);
                    _logger.LogInformation("Search index created: {IndexName}", _indexName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating search index");
                throw;
            }
        }

        public async Task<DocumentModel> IndexDocumentAsync(DocumentModel document)
        {
            try
            {
                // Generate embedding for the document content
                document.Embedding = await GenerateEmbeddingAsync(document.Content);
                
                // Index the document
                var batch = IndexDocumentsBatch.Upload(new[] { document });
                var result = await _searchClient.IndexDocumentsAsync(batch);
                
                _logger.LogInformation("Document indexed successfully: {DocumentTitle}", document.Title);
                return document;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error indexing document");
                throw;
            }
        }

        public async Task<IEnumerable<DocumentModel>> GetAllDocumentsAsync()
        {
            try
            {
                var options = new SearchOptions 
                { 
                    Size = 100,
                    Select = { "Id", "Title", "Content", "CreatedAt" }
                };
                var results = await _searchClient.SearchAsync<DocumentModel>("*", options);
                
                return results.Value.GetResults().Select(r => r.Document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving documents");
                throw;
            }
        }

        public async Task<IEnumerable<DocumentModel>> SearchDocumentsAsync(string query, int topK = 3)
        {
            try
            {
                // Generate embedding for the query
                var queryEmbedding = await GenerateEmbeddingAsync(query);
                
                // Create vector query
                var vectorQuery = new VectorizedQuery(queryEmbedding)
                {
                    KNearestNeighborsCount = topK,
                    Fields = { "Embedding" }
                };
                
                var options = new SearchOptions
                {
                    Size = topK,
                    Select = { "Id", "Title", "Content" },
                    VectorSearch = new VectorSearchOptions
                    {
                        Queries = { vectorQuery }
                    }
                };
                
                var results = await _searchClient.SearchAsync<DocumentModel>("", options);
                return results.Value.GetResults().Select(r => r.Document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching documents: {Message}", ex.Message);
                throw;
            }
        }

        public async Task<string> GenerateRAGContextAsync(string query, int topK = 3, float threshold = 0.7f)
        {
            try
            {
                var documents = await SearchDocumentsAsync(query, topK);
                if (!documents.Any())
                {
                    return string.Empty;
                }

                var contextBuilder = new System.Text.StringBuilder();
                contextBuilder.AppendLine("CONTEXT INFORMATION:");
                
                foreach (var doc in documents)
                {
                    contextBuilder.AppendLine($"Document: {doc.Title}");
                    contextBuilder.AppendLine(doc.Content);
                    contextBuilder.AppendLine();
                }

                contextBuilder.AppendLine("Answer the user's question based on the above context information.");
                return contextBuilder.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating RAG context");
                return string.Empty;
            }
        }

        private async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                var response = await _openAIClient.GetEmbeddingsAsync(
                    _embeddingDeployment,
                    new EmbeddingsOptions(text));
                
                return response.Value.Data[0].Embedding.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding");
                throw;
            }
        }
    }
}