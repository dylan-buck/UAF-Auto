using Serilog;
using SageBOI.Api.Configuration;
using SageBOI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/application.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .WriteTo.File("logs/errors.log",
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Sage 100 settings
builder.Services.Configure<SageConfiguration>(
    builder.Configuration.GetSection("Sage"));

// Register application services
builder.Services.AddSingleton<IProvideXSessionManager, ProvideXSessionManager>();
builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();

app.MapControllers();
app.MapHealthChecks("/health");

try
{
    Log.Information("Starting Sage BOI Service");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

