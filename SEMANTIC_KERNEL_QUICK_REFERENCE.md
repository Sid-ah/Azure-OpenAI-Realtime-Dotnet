# Semantic Kernel Quick Reference

## For Developers Working with This Project

### Key Concepts

**Semantic Kernel** = AI orchestration framework that organizes AI capabilities into reusable plugins
**Plugin** = Collection of related AI functions (e.g., IntentClassificationPlugin)  
**Function** = Individual AI capability within a plugin (e.g., ClassifyIntentAsync)
**Kernel** = Central coordinator that manages plugins and AI service connections

### Plugin Architecture Summary

```
Kernel
├── IntentClassificationPlugin
│   └── ClassifyIntentAsync() → "STATISTICAL" | "CONVERSATIONAL"
├── QueryRewritePlugin  
│   └── RewriteQueryAsync() → Enhanced query with context
├── SqlGenerationPlugin
│   └── GenerateSqlQueryAsync() → Executable SQL statement
└── DatabasePlugin
    └── ExecuteQueryAsync() → JSON formatted results
```

### How to Use Plugins

#### 1. Invoke a Plugin Function
```csharp
// Get plugin reference
var plugin = _kernel.Plugins["PluginName"];

// Invoke function with parameters
var result = await _kernel.InvokeAsync(plugin["FunctionName"], new()
{
    ["parameterName"] = parameterValue,
    ["anotherParam"] = anotherValue
});

// Get result
string output = result.ToString();
```

#### 2. Create a New Plugin
```csharp
public class MyCustomPlugin
{
    [KernelFunction, Description("What this function does")]
    public async Task<string> MyFunctionAsync(
        [Description("Parameter description")] string input,
        Kernel kernel)
    {
        var prompt = $"Process this input: {input}";
        var result = await kernel.InvokePromptAsync(prompt);
        return result.ToString();
    }
}
```

#### 3. Register New Plugin
```csharp
// In Program.cs
kernel.ImportPluginFromType<MyCustomPlugin>();

// Or with dependency injection
kernel.ImportPluginFromObject(new MyCustomPlugin(injectedService));
```

### Current Data Flow

1. **User Query** → Controller receives query
2. **Intent Classification** → Determine if statistical or conversational
3. **Query Enhancement** → Add conversation context if needed
4. **SQL Generation** → Convert to executable SQL (if statistical)
5. **Database Execution** → Run SQL and format results
6. **Response** → Return to frontend

### Configuration

```csharp
// Kernel setup (Program.cs)
var kernel = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey)
    .Build();

// Plugin registration
kernel.ImportPluginFromType<IntentClassificationPlugin>();
kernel.ImportPluginFromType<QueryRewritePlugin>();
kernel.ImportPluginFromType<SqlGenerationPlugin>();
```

### Error Handling

```csharp
try
{
    var result = await _kernel.InvokeAsync(plugin["Function"], parameters);
    return result.ToString();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Error in plugin execution");
    throw;
}
```

### Debugging Tips

1. **Check Plugin Registration**: Ensure all plugins are imported in Program.cs
2. **Validate Parameters**: Function parameters must match exactly
3. **Log Plugin Calls**: Add logging to track plugin invocations
4. **Test Plugins Independently**: Unit test each plugin function

### Adding New AI Capabilities

1. Create new plugin class with `[KernelFunction]` methods
2. Register plugin in Program.cs
3. Invoke from service using `_kernel.InvokeAsync()`
4. Handle results and errors appropriately

### Best Practices

- **Single Responsibility**: Each plugin should handle one AI capability
- **Clear Descriptions**: Use descriptive annotations for functions and parameters  
- **Error Handling**: Always wrap kernel calls in try-catch blocks
- **Logging**: Log inputs, outputs, and errors for debugging
- **Testing**: Unit test plugins independently from the kernel

### Quick Troubleshooting

| Issue | Solution |
|-------|----------|
| Plugin not found | Check `kernel.ImportPluginFromType<>()` in Program.cs |
| Function not found | Verify function name and `[KernelFunction]` attribute |
| Parameter mismatch | Ensure parameter names match function signature |
| Azure OpenAI errors | Validate configuration in appsettings.json |
| Null reference | Check for proper dependency injection setup |

### Performance Considerations

- Kernel initialization is expensive - do once at startup
- Plugin functions are lightweight - minimal overhead per call
- Database schema extraction happens per SQL generation call
- JSON serialization for results is optimized for LLM consumption
