using Microsoft.Extensions.Hosting.WindowsServices;
using Serilog;
using Serilog.Events;
using UAFMiddleware.Configuration;
using UAFMiddleware.Security;
using UAFMiddleware.Services;

// Configure Serilog early for startup logging
var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "uaf-middleware-.log");
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(logPath, 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.EventLog("UAF Sage Middleware", manageEventSource: true, restrictedToMinimumLevel: LogEventLevel.Warning)
    .CreateLogger();

try
{
    Log.Information("===========================================");
    Log.Information("UAF Sage Middleware Service Starting");
    Log.Information("===========================================");

    var builder = WebApplication.CreateBuilder(args);

    // Add local settings file (for credentials - not committed to Git)
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
    
    // Environment variables override everything (Sage__Username, Sage__Password, etc.)
    builder.Configuration.AddEnvironmentVariables();

    // Configure as Windows Service
    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "UAF Sage Middleware";
    });

    // Use Serilog
    builder.Host.UseSerilog();

    // Add configuration
    builder.Services.Configure<SageConfiguration>(
        builder.Configuration.GetSection("Sage"));
    builder.Services.Configure<ApiConfiguration>(
        builder.Configuration.GetSection("Api"));

    // Add services
    builder.Services.AddSingleton<IProvideXSessionManager, ProvideXSessionManager>();
    builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();
    builder.Services.AddScoped<ISalesOrderQueryService, SalesOrderQueryService>();
    builder.Services.AddScoped<ICustomerService, CustomerService>();
    builder.Services.AddScoped<ICustomerAccountService, CustomerAccountService>();
    builder.Services.AddScoped<IInventoryService, InventoryService>();
    builder.Services.AddScoped<IItemService, ItemService>();
    builder.Services.AddScoped<IVendorService, VendorService>();
    builder.Services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
    builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
    builder.Services.AddHostedService<HealthMonitorService>();

    // Add controllers
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "UAF Sage Middleware API", Version = "v1" });
    });

    // Configure Kestrel
    builder.WebHost.ConfigureKestrel(options =>
    {
        var apiConfig = builder.Configuration.GetSection("Api").Get<ApiConfiguration>();
        var port = apiConfig?.Port ?? 3000;
        options.ListenAnyIP(port);
        Log.Information("API configured to listen on port {Port}", port);
    });

    var app = builder.Build();

    // Middleware pipeline
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.Use(ApiKeyAuthenticationMiddleware);

    app.MapControllers();

    Log.Information("UAF Sage Middleware Service initialized successfully");
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "UAF Sage Middleware Service terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("UAF Sage Middleware Service stopped");
    Log.CloseAndFlush();
}

static async Task ApiKeyAuthenticationMiddleware(HttpContext context, Func<Task> next)
{
    // Skip auth for health endpoints
    if (context.Request.Path.StartsWithSegments("/health"))
    {
        await next();
        return;
    }

    var apiConfig = context.RequestServices.GetRequiredService<IConfiguration>()
        .GetSection("Api").Get<ApiConfiguration>();

    if (apiConfig == null ||
        (string.IsNullOrEmpty(apiConfig.ApiKey) && (apiConfig.ApiKeys?.Count ?? 0) == 0))
    {
        context.Items["ApiScopeResult"] = new ApiScopeResult
        {
            IsAuthenticated = true,
            ClientName = "development",
            Scopes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ApiScopes.Read,
                ApiScopes.Create,
                ApiScopes.Modify,
                ApiScopes.Finance,
                ApiScopes.Admin
            }
        };
        await next();
        return;
    }

    if (!context.Request.Headers.TryGetValue("X-API-Key", out var providedKey))
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key" });
        return;
    }

    var scopeResult = ApiScopePolicy.ResolveScopes(apiConfig, providedKey.ToString());
    if (!scopeResult.IsAuthenticated)
    {
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing API key" });
        return;
    }

    context.Items["ApiScopeResult"] = scopeResult;
    await next();
}
