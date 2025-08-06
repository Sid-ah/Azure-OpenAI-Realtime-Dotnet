# Semantic Kernel Integration Documentation

## Overview

This project has been updated to use **Microsoft Semantic Kernel** for better structured AI function calling and plugin management. Semantic Kernel provides a clean, modular approach to organizing AI capabilities into reusable plugins.

## Architecture Changes

### Before Semantic Kernel
The original implementation used direct Azure OpenAI client calls with hardcoded prompts:
- Direct `ChatClient.CompleteChatAsync()` calls
- Static prompt strings in `CorePrompts.cs`
- Tightly coupled AI logic in `AzureOpenAiService.cs`

### After Semantic Kernel
The new implementation uses a plugin-based architecture:
- **Kernel** orchestrates AI operations
- **Plugins** encapsulate specific AI capabilities
- **Functions** within plugins handle individual tasks
- Clean separation of concerns

## Plugin Architecture

### 1. Intent Classification Plugin (`IntentClassificationPlugin.cs`)

**Purpose**: Determines whether a user query requires database lookup (STATISTICAL) or general conversation (CONVERSATIONAL).

```csharp
[KernelFunction, Description("Classifies a user query as either statistical or conversational")]
public async Task<string> ClassifyIntentAsync(
    [Description("The full conversation history")] string conversationHistory,
    [Description("The current user query to classify")] string userQuery,
    Kernel kernel)
```

**Flow**:
1. Receives conversation history and current query
2. Uses contextual prompting to classify intent
3. Returns "STATISTICAL" or "CONVERSATIONAL"

### 2. Query Rewrite Plugin (`QueryRewritePlugin.cs`)

**Purpose**: Enhances user queries by adding context from conversation history for better SQL generation.

```csharp
[KernelFunction, Description("Rewrites a user query to include context from conversation history")]
public async Task<string> RewriteQueryAsync(
    [Description("The full conversation history")] string conversationHistory,
    [Description("The current user query to rewrite")] string userQuery,
    Kernel kernel)
```

**Example**:
- Original: "How many points did he score?"
- Rewritten: "How many points did Max Verstappen score in the 2023 season?"

### 3. SQL Generation Plugin (`SqlGenerationPlugin.cs`)

**Purpose**: Converts natural language queries into executable SQL statements.

```csharp
[KernelFunction, Description("Generates a SQL query from natural language based on database schema")]
public async Task<string> GenerateSqlQueryAsync(
    [Description("The natural language query to convert to SQL")] string naturalLanguageQuery,
    [Description("The database schema in JSON format")] string jsonSchema,
    [Description("Optional: previous SQL query that failed")] string? previousSqlQuery,
    [Description("Optional: error message from previous SQL execution")] string? sqlErrorMessage,
    Kernel kernel)
```

**Features**:
- Schema-aware SQL generation
- Error recovery with retry logic
- Single-line SQL output format

### 4. Database Plugin (`DatabasePlugin.cs`)

**Purpose**: Executes SQL queries against the Formula One database and returns formatted results.

```csharp
[KernelFunction, Description("Executes a SQL query against the Formula One database")]
public async Task<string> ExecuteQueryAsync(
    [Description("The SQL query to execute")] string sqlQuery)
```

**Features**:
- JSON-formatted results for LLM consumption
- Error handling and logging
- Connection management

## Configuration & Initialization

### Program.cs Setup

```csharp
// Configure Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();

// Add Azure OpenAI chat completion service
kernelBuilder.AddAzureOpenAIChatCompletion(
    deploymentName: chatDeploymentName,
    endpoint: $"https://{resourceName}.openai.azure.com",
    apiKey: apiKey);

// Build the kernel
var kernel = kernelBuilder.Build();

// Add plugins to the kernel
kernel.ImportPluginFromType<IntentClassificationPlugin>();
kernel.ImportPluginFromType<QueryRewritePlugin>();
kernel.ImportPluginFromType<SqlGenerationPlugin>();

// Register as singleton
builder.Services.AddSingleton(kernel);
```

### Service Integration

The `AzureOpenAiService` now receives a configured `Kernel` instance and uses it to invoke plugins:

```csharp
public AzureOpenAiService(IConfiguration configuration, ILogger<AzureOpenAIController> logger, Kernel kernel)
{
    _kernel = kernel;
    // ... other initialization
}
```

## Data Flow with Semantic Kernel

### Statistical Query Processing

1. **Intent Classification**
   ```csharp
   var intentPlugin = _kernel.Plugins["IntentClassificationPlugin"];
   var result = await _kernel.InvokeAsync(intentPlugin["ClassifyIntentAsync"], new()
   {
       ["conversationHistory"] = conversationHistory,
       ["userQuery"] = userQuery
   });
   ```

2. **Query Enhancement**
   ```csharp
   var queryRewritePlugin = _kernel.Plugins["QueryRewritePlugin"];
   var result = await _kernel.InvokeAsync(queryRewritePlugin["RewriteQueryAsync"], new()
   {
       ["conversationHistory"] = conversationHistory,
       ["userQuery"] = userQuery
   });
   ```

3. **SQL Generation**
   ```csharp
   var sqlGenerationPlugin = _kernel.Plugins["SqlGenerationPlugin"];
   var result = await _kernel.InvokeAsync(sqlGenerationPlugin["GenerateSqlQueryAsync"], new()
   {
       ["naturalLanguageQuery"] = rewrittenQuery,
       ["jsonSchema"] = jsonSchema,
       ["previousSqlQuery"] = previousGeneratedSqlQuery,
       ["sqlErrorMessage"] = sqlErrorMessage
   });
   ```

4. **Database Execution**
   ```csharp
   var databasePlugin = _kernel.Plugins["DatabasePlugin"];
   var result = await _kernel.InvokeAsync(databasePlugin["ExecuteQueryAsync"], new()
   {
       ["sqlQuery"] = generatedSql
   });
   ```

## Benefits of Semantic Kernel Integration

### 1. **Modularity**
- Each AI capability is encapsulated in its own plugin
- Easy to test, maintain, and extend individual components
- Clear separation of concerns

### 2. **Reusability**
- Plugins can be reused across different parts of the application
- Easy to share plugins between projects
- Standard interface for AI functions

### 3. **Maintainability**
- Centralized kernel configuration
- Consistent error handling across plugins
- Easy to add new AI capabilities

### 4. **Extensibility**
- Simple to add new plugins for additional functionality
- Built-in support for function composition
- Easy integration with external services

### 5. **Type Safety**
- Strongly typed function parameters
- Compile-time validation of plugin interfaces
- Better IntelliSense support

## Error Handling & Retry Logic

The Semantic Kernel integration maintains the existing retry logic for SQL generation:

```csharp
const int MaxRetries = 3;
int attemptCount = 0;
var generatedSqlQuery = string.Empty;
var errorMessage = string.Empty;

while (attemptCount < MaxRetries) {
    try
    {
        generatedSqlQuery = await _azureOpenAiService.GenerateSqlQuery(
            rewrittenQuery, 
            generatedSqlQuery, 
            errorMessage);
        
        var results = await _databaseService.ExecuteQueryAsync(generatedSqlQuery);
        return Ok(results);
    }
    catch (Exception ex)
    {
        attemptCount++;
        errorMessage = ex.Message;
    }
}
```

## Plugin Function Descriptions

Each plugin function uses semantic descriptions to help the Semantic Kernel understand their purpose:

```csharp
[KernelFunction, Description("Clear description of what this function does")]
public async Task<string> FunctionName(
    [Description("Parameter description")] string parameter,
    Kernel kernel)
```

These descriptions are used by:
- The Semantic Kernel for function discovery
- Future AI orchestration capabilities
- Documentation generation
- IDE IntelliSense

## Configuration Requirements

### NuGet Packages Added
```xml
<PackageReference Include="Microsoft.SemanticKernel" Version="1.35.0" />
<PackageReference Include="Microsoft.SemanticKernel.Connectors.AzureOpenAI" Version="1.35.0" />
```

### Configuration Settings
The same Azure OpenAI configuration is used:
```json
{
  "AzureOpenAI": {
    "ResourceName": "your-resource",
    "ChatDeploymentName": "gpt-4o",
    "ApiKey": "your-key"
  }
}
```

## Future Enhancements

With Semantic Kernel in place, the project can easily be extended with:

1. **Memory Plugins**: For conversation persistence
2. **Web Search Plugins**: For external data retrieval
3. **Document Plugins**: For PDF/document processing
4. **Planning Plugins**: For multi-step AI workflows
5. **Custom Connectors**: For other AI services

## Performance Considerations

- Kernel initialization is done once at startup
- Plugins are lightweight and stateless
- Function invocation has minimal overhead
- JSON serialization for database results is optimized

## Testing Strategy

With Semantic Kernel, testing becomes more focused:

1. **Unit Test Individual Plugins**: Test each plugin function in isolation
2. **Mock Kernel Dependencies**: Easy to mock kernel for testing
3. **Integration Tests**: Test plugin composition and data flow
4. **Performance Tests**: Measure plugin execution times

## Troubleshooting

### Common Issues

1. **Plugin Not Found**: Ensure plugin is imported in Program.cs
2. **Function Not Found**: Check function name and plugin registration
3. **Parameter Type Mismatch**: Verify parameter types match function signature
4. **Configuration Missing**: Validate Azure OpenAI settings

### Debugging Tips

- Enable detailed logging for Semantic Kernel operations
- Use breakpoints in plugin functions
- Validate kernel plugin registration at startup
- Check function descriptions and parameter mappings

This Semantic Kernel integration provides a solid foundation for future AI enhancements while maintaining the existing Formula One query capabilities.
