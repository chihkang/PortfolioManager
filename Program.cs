using PortfolioManager.Configuration;
using PortfolioManager.Controllers;
using PortfolioManager.Jobs;
using PortfolioManager.Services;
using Quartz;

var builder = WebApplication.CreateBuilder(args);
// 取得 Railway 的 PORT 環境變數
var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// 配置應用程式使用的 URL
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// 配置 MongoDB 設定
builder.Services.Configure<MongoDbSettings>(options =>
{
    // 優先使用環境變數，如果沒有則使用 appsettings.json
    options.ConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
                               ?? builder.Configuration.GetSection("MongoDbSettings:ConnectionString").Value;

    options.DatabaseName = Environment.GetEnvironmentVariable("MONGODB_DATABASE")
                           ?? builder.Configuration.GetSection("MongoDbSettings:DatabaseName").Value;
});

builder.Services.Configure<PortfolioUpdateOptions>(
    builder.Configuration.GetSection("PortfolioUpdate"));

// 添加 MediatR
builder.Services.AddMediatR(cfg => { cfg.RegisterServicesFromAssembly(typeof(Program).Assembly); });
builder.Services.AddHttpClient<ExchangeRateController>();
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
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
builder.Services.AddScoped<PortfolioDailyValueService>();
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddScoped<PortfolioUpdateService>();
builder.Services.AddScoped<PortfolioCacheService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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