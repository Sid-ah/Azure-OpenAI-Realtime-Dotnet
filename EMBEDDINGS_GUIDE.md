# Azure OpenAI Embeddings Integration Guide

## Overview

This project includes advanced embeddings functionality to optimize Natural Language to SQL (NL2SQL) query performance by intelligently selecting relevant database tables based on semantic similarity. The embeddings implementation uses Azure OpenAI's embedding models to reduce context size and improve SQL generation accuracy.

## Architecture

The embeddings system works as a "table gating" mechanism that filters database schemas before SQL generation:

```
User Query → Embedding Generation → Table Similarity Calculation → Filtered Schema → SQL Generation
```

### Key Components

1. **Embedding Client**: Utilizes Azure OpenAI's `EmbeddingClient` from the latest SDK
2. **Table Descriptions**: Creates semantic representations of database tables including column names
3. **Similarity Calculation**: Computes cosine similarity between query and table embeddings
4. **Schema Filtering**: Selects top-relevant tables for SQL generation

## Implementation Details

### Service Configuration

The embeddings functionality is integrated into the `AzureOpenAiService` class with the following key components:

```csharp
private readonly EmbeddingClient? _embeddingClient;
private readonly string? _embeddingDeploymentName;
```

### Initialization

The embedding client is initialized in the constructor when an embedding deployment is configured:

```csharp
// Initialize Azure OpenAI Embeddings client (if configured)
var embeddingModel = configuration["AzureOpenAI:EmbeddingDeploymentName"];
if (!string.IsNullOrEmpty(embeddingModel))
{
    _embeddingClient = azureOpenAIClient.GetEmbeddingClient(embeddingModel);
    _embeddingDeploymentName = embeddingModel;
}
```

### Table Relevance Detection

The `GetRelevantTablesAsync` method performs the core embeddings functionality:

1. **Query Embedding**: Generates embedding for the user query
2. **Table Embeddings**: Creates embeddings for each table description (table name + column names)
3. **Similarity Calculation**: Computes cosine similarity between query and table embeddings
4. **Table Selection**: Returns top 2 most relevant tables with similarity above 0.2 threshold

## Configuration

### appsettings.Local.json

Add the embedding deployment configuration:

```json
{
  "AzureOpenAI": {
    "ResourceName": "your-azure-openai-resource-name",
    "RealtimeDeploymentName": "gpt-4o-realtime-preview",
    "ChatDeploymentName": "gpt-4o",
    "EmbeddingDeploymentName": "text-embedding-3-small",
    "ApiKey": "your-azure-openai-api-key",
    "ApiVersion": "2025-04-01-preview"
  },
  "DatabaseConnection": "your-connection-string",
  "Nl2SqlConfig": {
    "database": {
      "description": "Your database description",
      "schemas": [
        {
          "name": "dbo",
          "tables": ["Table1", "Table2", "Table3"]
        }
      ]
    }
  }
}
```

### Supported Embedding Models

- `text-embedding-3-small` (recommended for cost-effectiveness)
- `text-embedding-3-large` (higher accuracy, higher cost)
- `text-embedding-ada-002` (legacy, still supported)

## API Usage

### Latest Azure.AI.OpenAI SDK (v2.2.0-beta.4)

The implementation uses the latest Azure OpenAI SDK with the following key changes from previous versions:

```csharp
// Generate embedding for user query
var queryEmbeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(userQuery);
var queryEmbedding = queryEmbeddingResponse.Value.ToFloats();

// Generate embedding for table description
var tableEmbeddingResponse = await _embeddingClient.GenerateEmbeddingAsync(tableDescText);
var tableEmbedding = tableEmbeddingResponse.Value.ToFloats();
```

### Key SDK Changes

1. **Client Type**: Uses `EmbeddingClient` instead of generic `OpenAIClient`
2. **Method Name**: `GenerateEmbeddingAsync()` instead of `GetEmbeddingsAsync()`
3. **Response Format**: `.Value.ToFloats()` instead of `.Value.Data[0].Embedding`
4. **Initialization**: `azureOpenAIClient.GetEmbeddingClient(deploymentName)` pattern

## Algorithm Details

### Cosine Similarity Calculation

The system computes cosine similarity between embeddings using the formula:

```
similarity = (A · B) / (||A|| × ||B||)
```

Implementation:
```csharp
float dot = 0, normQ = 0, normT = 0;
for (int i = 0; i < queryVector.Length; i++)
{
    dot += queryVector[i] * tableVector[i];
    normQ += queryVector[i] * queryVector[i];
    normT += tableVector[i] * tableVector[i];
}
float cosineSim = dot / ((float)Math.Sqrt(normQ) * (float)Math.Sqrt(normT));
```

### Table Selection Logic

1. **Similarity Threshold**: 0.2 minimum similarity score
2. **Top-N Selection**: Maximum 2 tables to keep prompts concise
3. **Fallback**: Always includes the highest-scoring table regardless of threshold
4. **Schema Filtering**: Creates reduced schema containing only selected tables

## Performance Benefits

### Context Reduction

- **Without Embeddings**: Full database schema sent to LLM (potentially thousands of tokens)
- **With Embeddings**: Only 1-2 relevant tables included (significantly reduced context)

### Accuracy Improvement

- **Semantic Matching**: Tables are selected based on meaning, not just keyword matching
- **Context Preservation**: Reduced noise in SQL generation prompts
- **Cost Optimization**: Fewer tokens in LLM requests

## Monitoring and Debugging

### Logging

The system logs embedding-gated table selection:

```csharp
_logger.LogInformation($"Embedding-gated tables for query '{userQuery}': {string.Join(", ", selectedTables)}");
```

### Example Log Output

```
info: Embedding-gated tables for query 'Who won the most races in 2023?': F1Records, F1CircuitWinners
info: Embedding-gated tables for query 'Show me constructor championships': F1ConstructorChampions
```

## Error Handling

### Fallback Behavior

If embeddings are not configured or fail:

```csharp
if (_embeddingClient == null || string.IsNullOrEmpty(_embeddingDeploymentName))
    return _nl2SqlConfig.Database.Schemas.SelectMany(s => s.Tables).ToArray();
```

The system gracefully falls back to including all tables in the schema.

### Exception Handling

- **Network Issues**: Automatic fallback to full schema
- **Model Unavailability**: Graceful degradation with logging
- **Invalid Embeddings**: Error logging with fallback behavior

## Best Practices

### Table Descriptions

Create meaningful table descriptions by including:

1. **Table Name**: Clear, descriptive table names
2. **Column Context**: Include column names in the description
3. **Business Context**: Add domain-specific information

Example table description format:
```
"dbo.F1Records: DriverName, TeamName, Season, Points, Championships, FastestLaps"
```

### Embedding Model Selection

- **Development**: Use `text-embedding-3-small` for cost-effectiveness
- **Production**: Consider `text-embedding-3-large` for higher accuracy
- **Legacy Systems**: `text-embedding-ada-002` for compatibility

### Performance Tuning

1. **Threshold Adjustment**: Modify the 0.2 similarity threshold based on your data
2. **Table Limit**: Adjust the `Take(2)` limit based on your schema complexity
3. **Caching**: Consider caching table embeddings for frequently accessed tables

## Troubleshooting

### Common Issues

1. **No Tables Selected**:
   - Check similarity threshold (may be too high)
   - Verify table descriptions are meaningful
   - Review embedding model configuration

2. **Poor Table Selection**:
   - Improve table and column naming conventions
   - Add descriptive metadata to database tables
   - Consider using a more powerful embedding model

3. **Performance Issues**:
   - Cache table embeddings if tables don't change frequently
   - Optimize database queries for column metadata
   - Consider reducing the number of tables processed

### Debugging Steps

1. **Enable Detailed Logging**:
   ```json
   "Logging": {
     "LogLevel": {
       "realtime_api_dotnet.Services": "Information"
     }
   }
   ```

2. **Test Similarity Scores**:
   - Log similarity scores for analysis
   - Manually verify table selections
   - Adjust threshold based on observed scores

3. **Validate Embeddings**:
   - Test embedding generation for sample queries
   - Verify table descriptions are being created correctly
   - Check that embeddings are being generated successfully

## Migration Guide

### From Previous SDK Versions

If migrating from earlier Azure OpenAI SDK versions:

1. **Update Package**: Ensure `Azure.AI.OpenAI` is version 2.2.0-beta.4 or later
2. **Update Client Initialization**: Use `GetEmbeddingClient()` pattern
3. **Update API Calls**: Replace `GetEmbeddingsAsync()` with `GenerateEmbeddingAsync()`
4. **Update Response Handling**: Use `.Value.ToFloats()` instead of `.Value.Data[0].Embedding`

### Configuration Changes

Ensure your configuration includes the embedding deployment:

```json
"EmbeddingDeploymentName": "text-embedding-3-small"
```

## Future Enhancements

### Potential Improvements

1. **Embedding Caching**: Cache table embeddings to reduce API calls
2. **Dynamic Threshold**: Adjust similarity threshold based on query complexity
3. **Hybrid Matching**: Combine embeddings with keyword-based filtering
4. **Model Fine-tuning**: Fine-tune embeddings for domain-specific terminology

### Performance Monitoring

Consider implementing:

1. **Metrics Collection**: Track embedding API latency and costs
2. **A/B Testing**: Compare SQL generation accuracy with/without embeddings
3. **Usage Analytics**: Monitor which tables are most frequently selected

---

**Last Updated**: August 2025  
**Azure OpenAI SDK Version**: 2.2.0-beta.4  
**Supported Embedding Models**: text-embedding-3-small, text-embedding-3-large, text-embedding-ada-002
