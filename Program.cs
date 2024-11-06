using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using PortfolioManager.Configuration;
using PortfolioManager.Events;
using PortfolioManager.Services;

var builder = WebApplication.CreateBuilder(args);
// 取得 Railway 的 PORT 環境變數
var port = Environment.GetEnvironmentVariable("PORT") ?? "3000";

// 配置應用程式使用的 URL
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.Configure<PortfolioUpdateOptions>(
    builder.Configuration.GetSection("PortfolioUpdate"));
// 添加 MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// 註冊服務
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddScoped<PortfolioUpdateService>();
builder.Services.AddScoped<PortfolioCacheService>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache(); // Add this line to register IMemoryCache
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment()|| app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Portfolio Manager API V1");
        c.RoutePrefix = string.Empty; // This makes Swagger UI available at the root URL
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();