using System.Reflection;
using Azure.Identity;
using BlobCopy.API.Extensions;
using BlobCopy.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// Configuration
// ============================================================================

var storageAccountUri = new Uri(builder.Configuration["Azure:Storage:AccountUri"] 
    ?? throw new InvalidOperationException("Azure:Storage:AccountUri is required"));

var instrumentationKey = builder.Configuration["ApplicationInsights:InstrumentationKey"];
var environment = builder.Environment;

// ============================================================================
// Services Registration
// ============================================================================

// Add ASP.NET Core services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add API versioning
builder.Services.AddApiVersioning();

// Add health checks
builder.Services.AddHealthChecks();

// Add logging and observability
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

if (!string.IsNullOrWhiteSpace(instrumentationKey))
{
    builder.Services.AddApplicationInsightsTelemetry(instrumentationKey);
    builder.Logging.AddApplicationInsights();
}

// Add SignalR for real-time updates
builder.Services.AddBlobCopySignalR();

// Add blob copy business services (validation, copy, progress notification)
builder.Services.AddBlobCopyServices();

// Add Azure Storage services
if (environment.IsDevelopment())
{
    // Development: Use connection string (Azurite or Azure Storage Emulator)
    var connectionString = builder.Configuration["Azure:Storage:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        builder.Services.AddAzureStorageServicesWithConnectionString(connectionString);
    }
    else
    {
        builder.Services.AddAzureStorageServices(storageAccountUri);
    }
}
else
{
    // Production: Use DefaultAzureCredential (Managed Identity)
    builder.Services.AddAzureStorageServices(storageAccountUri);
}

// Add CORS for frontend requests
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000" };

        if (environment.IsDevelopment())
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
    });
});

// Add authentication (Azure Entra ID / JWT Bearer)
builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Azure:Authority"]
            ?? "https://login.microsoftonline.com/{TenantId}";
        options.Audience = builder.Configuration["Azure:Audience"]
            ?? builder.Configuration["Azure:ClientId"];
    });

// Add authorization
builder.Services.AddAuthorization();

// Add Swagger/OpenAPI documentation
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Blob Copy API",
        Version = "v1",
        Description = "API for copying blobs in Azure Blob Storage with real-time progress tracking",
        Contact = new() { Name = "Development Team" },
        License = new() { Name = "MIT" }
    });

    // Include XML comments from API controllers
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ============================================================================
// Build Application
// ============================================================================

var app = builder.Build();

// ============================================================================
// Middleware Pipeline
// ============================================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Blob Copy API v1");
        options.RoutePrefix = "api-docs";
    });
}

// Enable HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Enable CORS
app.UseCors("AllowFrontend");

// Add request logging middleware
app.UseMiddleware<RequestLoggingMiddleware>();

// Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

// Map SignalR hubs
app.MapBlobCopyHub("/signalr/blob-copy-hub");

// ============================================================================
// Run Application
// ============================================================================

app.Logger.LogInformation("Starting Blob Copy API in {Environment} environment", app.Environment.EnvironmentName);
app.Logger.LogInformation("Storage Account URI: {StorageUri}", storageAccountUri);

await app.RunAsync();
