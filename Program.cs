using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using PortfolioManager.Configuration;
using PortfolioManager.Events;
using PortfolioManager.Services;
using PortfolioManager.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.Configure<PortfolioUpdateOptions>(
    builder.Configuration.GetSection("PortfolioUpdate"));
builder.Services.AddScoped<INotificationHandler<StockPriceUpdatedEvent>, StockPriceUpdateHandler>();
// 添加 MediatR
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
});

// 使用 Memory Cache（開發環境）或 Redis（生產環境）
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "PortfolioManager:";
    });
}

// 註冊服務
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddScoped<INotificationHandler<ExchangeRateUpdatedEvent>, ExchangeRateUpdateHandler>();
builder.Services.AddScoped<PortfolioCalculationService>();
builder.Services.AddScoped<PortfolioUpdateService>();
builder.Services.AddScoped<PortfolioCacheService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();