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

// Configure Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();

// Get Azure OpenAI configuration
var resourceName = builder.Configuration["AzureOpenAI:ResourceName"];
var apiKey = builder.Configuration["AzureOpenAI:ApiKey"];
var chatDeploymentName = builder.Configuration["AzureOpenAI:ChatDeploymentName"];

if (string.IsNullOrEmpty(resourceName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(chatDeploymentName))
{
    throw new InvalidOperationException("Azure OpenAI configuration is incomplete. Check ResourceName, ApiKey, and ChatDeploymentName.");
}

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

// Register the configured kernel as a singleton
builder.Services.AddSingleton(kernel);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<DatabaseService>();

// Register DatabasePlugin with dependency injection
builder.Services.AddSingleton<DatabasePlugin>();

// Register the plugin with the kernel after DatabaseService is available
builder.Services.AddSingleton<AzureOpenAiService>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<AzureOpenAIController>>();
    var kernelInstance = serviceProvider.GetRequiredService<Kernel>();
    var databaseService = serviceProvider.GetRequiredService<DatabaseService>();
    
    // Import the database plugin with the injected service
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