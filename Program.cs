using PortfolioManager;
using PortfolioManager.Configuration;
using PortfolioManager.Controllers;
using PortfolioManager.Jobs;
using PortfolioManager.Services;
using Quartz;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Behind reverse proxies (Zeabur), trust X-Forwarded-* so scheme/host are correct.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // Zeabur's proxy IPs are not known ahead of time.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Avoid noisy warning when HTTPS is terminated at the proxy.
builder.Services.AddHttpsRedirection(options => { options.HttpsPort = 443; });

// 配置 MongoDB 設定
builder.Services
    .AddOptions<MongoDbSettings>()
    .Bind(builder.Configuration.GetSection("MongoDbSettings"))
    .PostConfigure(options =>
    {
        options.ConnectionString ??=
            Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("MongoSettings__ConnectionString");

        options.DatabaseName ??=
            Environment.GetEnvironmentVariable("MONGODB_DATABASE")
            ?? Environment.GetEnvironmentVariable("MongoSettings__DatabaseName");
    });

builder.Services.Configure<PortfolioUpdateOptions>(
    builder.Configuration.GetSection("PortfolioUpdate"));

// 添加 MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>();
// 註冊服務
// 添加 Quartz
builder.Services.AddQuartz(q =>
{
    var jobKey = new JobKey("RecordDailyValueJob");

    q.AddJob<RecordDailyValueJob>(opts => opts.WithIdentity(jobKey));

    // 週一至週五 13:35 的觸發器
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("WeekdayTrigger")
        .WithCronSchedule("0 35 13 ? * MON-FRI", x => x
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")))
    );

    // 週六 05:35 的觸發器
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithIdentity("SaturdayTrigger")
        .WithCronSchedule("0 35 5 ? * SAT", x => x
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")))
    );

    // Stock Updater Job
    var stockUpdaterJobKey = new JobKey("StockUpdaterJob");
    q.AddJob<StockUpdaterJob>(opts => opts.WithIdentity(stockUpdaterJobKey).StoreDurably());

    // TW Market Triggers (Asia/Taipei)
    // 09:00 - 12:55
    q.AddTrigger(opts => opts
        .ForJob(stockUpdaterJobKey)
        .WithIdentity("TwStockMorningTrigger")
        .UsingJobData("stockType", "TW")
        .WithCronSchedule("0 0/5 9-12 ? * MON-FRI", x => x
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")))
    );
    // 13:00 - 13:30
    q.AddTrigger(opts => opts
        .ForJob(stockUpdaterJobKey)
        .WithIdentity("TwStockAfternoonTrigger")
        .UsingJobData("stockType", "TW")
        .WithCronSchedule("0 0-30/5 13 ? * MON-FRI", x => x
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei")))
    );

    // US Market Triggers (America/New_York) - Handles DST automatically
    // 09:30 - 09:55
    q.AddTrigger(opts => opts
        .ForJob(stockUpdaterJobKey)
        .WithIdentity("UsStockMorningTrigger")
        .UsingJobData("stockType", "US")
        .WithCronSchedule("0 30/5 9 ? * MON-FRI", x => x
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York")))
    );
    // 10:00 - 15:55
    q.AddTrigger(opts => opts
        .ForJob(stockUpdaterJobKey)
        .WithIdentity("UsStockDayTrigger")
        .UsingJobData("stockType", "US")
        .WithCronSchedule("0 0/5 10-15 ? * MON-FRI", x => x
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York")))
    );
    // 16:00
    q.AddTrigger(opts => opts
        .ForJob(stockUpdaterJobKey)
        .WithIdentity("UsStockCloseTrigger")
        .UsingJobData("stockType", "US")
        .WithCronSchedule("0 0 16 ? * MON-FRI", x => x
            .InTimeZone(TimeZoneInfo.FindSystemTimeZoneById("America/New_York")))
    );
});

// 添加 Quartz 托管服務
builder.Services.AddQuartzHostedService(options =>
{
    options.WaitForJobsToComplete = true;
    options.AwaitApplicationStarted = true;
});
builder.Services.AddScoped<PortfolioDailyValueService>();
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddScoped<PortfolioCacheService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseForwardedHeaders();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();