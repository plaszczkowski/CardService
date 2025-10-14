using CardActions.API;
using CardActions.API.BackgroundServices;
using CardActions.API.Diagnostics;
using CardActions.API.Middleware;
using CardActions.API.Telemetry;
using CardActions.API.Validators;
using CardActions.Application.Interfaces;
using CardActions.Application.Services;
using CardActions.Domain.Services;
using CardActions.Infrastructure;
using CardActions.Infrastructure.Configuration;
using CardActions.Infrastructure.Diagnostics;
using FluentValidation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var rabbitMqAssembly = AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "RabbitMQ.Client");

if (rabbitMqAssembly != null)
{
    Console.WriteLine($"[DIAGNOSTIC] RabbitMQ.Client runtime version: {rabbitMqAssembly.GetName().Version}");
    Console.WriteLine($"[DIAGNOSTIC] RabbitMQ.Client location: {rabbitMqAssembly.Location}");
}
else
{
    Console.WriteLine("[DIAGNOSTIC] RabbitMQ.Client NOT LOADED");
}

var builder = WebApplication.CreateBuilder(args);

// Bind EventBus configuration with validation
var eventBusOptions = builder.Configuration
    .GetSection(EventBusOptions.SectionName)
    .Get<EventBusOptions>() ?? new EventBusOptions();

    try
    {
        eventBusOptions.Validate(builder.Environment);
        builder.Services.AddSingleton(eventBusOptions);

        builder.Services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.AddDebug();
        });

        var startupLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Program>();
        startupLogger.LogInformation("Event Bus configured - Mode: {Mode}",
            eventBusOptions.UseInMemory ? "InMemory" : "RabbitMQ");
    }
    catch (InvalidOperationException ex)
    {
        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<Program>();
        logger.LogCritical(ex, "Invalid EventBus configuration. Application cannot start.");
        throw;
    }

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Card Actions API", Version = "v1" });
});

builder.Services.AddHealthChecks().AddCheck<SampleHealthCheck>("card_service_health");
builder.Services.AddValidatorsFromAssemblyContaining<CardActionsRequestValidator>();
builder.Services.AddHttpContextAccessor();

// Register layers
builder.Services.AddScoped<ICardActionPolicy, CardActionPolicy>();          // Domain Services
builder.Services.AddSingleton<ICardActionsMetrics, CardActionsMetrics>();   // Metrics
builder.Services.AddScoped<CardActionsService>();                           // Application Services

builder.Services.Configure<EventBusOptions>(builder.Configuration.GetSection("EventBus"));

// Pass configuration to Infrastructure layer
builder.Services.AddInfrastructure(eventBusOptions);

// OpenTelemetry with exporters
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddMeter("CardActions.API")
               .AddConsoleExporter();
    })
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddSource("CardActions.API")
               .AddConsoleExporter();
    });

builder.Services.Configure<TestConfiguration>(builder.Configuration.GetSection("TestConfiguration"));
builder.Services.AddHttpClient();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IStartupAudit, StartupAudit>();
    builder.Services.AddHostedService<ApiTestBackgroundService>();
}

var app = builder.Build();
if (app.Environment.IsDevelopment()) // Configure the HTTP request pipeline
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>(); // Global Middleware
app.UseAuthorization();
app.MapControllers(); // Map endpoints
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }