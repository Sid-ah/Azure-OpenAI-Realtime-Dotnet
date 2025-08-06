using realtime_api_dotnet.Services;
using realtime_api_dotnet.Plugins;
using realtime_api_dotnet.Controllers;
using Microsoft.SemanticKernel;
using System.Security.Authentication;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();

// Load configuration from appsettings.json and appsettings.local.json
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);

// =================== SEMANTIC KERNEL CONFIGURATION ===================
// Configure Semantic Kernel with Azure OpenAI integration
// This replaces direct Azure OpenAI client usage with a plugin-based architecture
var kernelBuilder = Kernel.CreateBuilder();

// Get Azure OpenAI configuration
var resourceName = builder.Configuration["AzureOpenAI:ResourceName"];
var apiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var chatDeploymentName = builder.Configuration["AzureOpenAI:ChatDeploymentName"];

if (string.IsNullOrEmpty(resourceName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(chatDeploymentName))
{
    throw new InvalidOperationException("Azure OpenAI configuration is incomplete. Check ResourceName, ApiKey, and ChatDeploymentName.");
}

// Add Azure OpenAI chat completion service to the kernel
// This enables all plugins to use the same Azure OpenAI connection
kernelBuilder.AddAzureOpenAIChatCompletion(
    deploymentName: chatDeploymentName,
    endpoint: $"https://{resourceName}.openai.azure.com",
    apiKey: apiKey);

// Build the kernel instance
var kernel = kernelBuilder.Build();

// =================== PLUGIN REGISTRATION ===================
// Import all AI capability plugins into the kernel
// These plugins encapsulate specific AI functions for modularity and reusability

// Intent Classification: Determines if query needs database lookup (STATISTICAL vs CONVERSATIONAL)
kernel.ImportPluginFromType<IntentClassificationPlugin>();

// Query Rewrite: Enhances user queries with conversation context for better SQL generation
kernel.ImportPluginFromType<QueryRewritePlugin>();

// SQL Generation: Converts natural language to SQL using database schema
kernel.ImportPluginFromType<SqlGenerationPlugin>();

// Register the configured kernel as a singleton for dependency injection
builder.Services.AddSingleton(kernel);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<DatabaseService>();

// Register DatabasePlugin with dependency injection
builder.Services.AddSingleton<DatabasePlugin>();

// =================== SERVICE REGISTRATION ===================
// Register AzureOpenAiService with Semantic Kernel integration
// This service orchestrates plugin calls for the complete NL2SQL pipeline
builder.Services.AddSingleton<AzureOpenAiService>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<AzureOpenAIController>>();
    var kernelInstance = serviceProvider.GetRequiredService<Kernel>();
    var databaseService = serviceProvider.GetRequiredService<DatabaseService>();
    
    // Import the database plugin with the injected service
    // This plugin handles SQL execution and result formatting
    kernelInstance.ImportPluginFromObject(new DatabasePlugin(databaseService));
    
    return new AzureOpenAiService(configuration, logger, kernelInstance);
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", builder =>
    {
        builder.WithOrigins("*")
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors("AllowReactApp");
app.UseAuthorization();
app.MapControllers();

app.Run();