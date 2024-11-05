using MongoDB.Driver;
using PortfolioManager.Events;
using PortfolioManager.Models;
using Microsoft.Extensions.Options;
using PortfolioManager.Configuration;

namespace PortfolioManager.Services
{
    public class PortfolioUpdateService
    {
        private readonly MongoDbService _mongoDbService;
        private readonly ILogger<PortfolioUpdateService> _logger;
        private readonly PortfolioCalculationService _calculationService;
        private readonly IOptions<PortfolioUpdateOptions> _options;

        public PortfolioUpdateService(
            MongoDbService mongoDbService,
            ILogger<PortfolioUpdateService> logger,
            PortfolioCalculationService calculationService,
            IOptions<PortfolioUpdateOptions> options)
        {
            _mongoDbService = mongoDbService;
            _logger = logger;
            _calculationService = calculationService;
            _options = options;
        }

        public async Task HandleStockPriceUpdated(StockPriceUpdatedEvent priceEvent)
        {
            try
            {
                _logger.LogInformation($"Starting portfolio updates for stock price change: {priceEvent.StockId}");

                // 1. 獲取受影響的投資組合
                var filter = Builders<Portfolio>.Filter.ElemMatch(
                    p => p.Stocks,
                    stock => stock.StockId == priceEvent.StockId
                );

                var portfolios = await _mongoDbService.Portfolios
                    .Find(filter)
                    .ToListAsync();

                if (!portfolios.Any())
                {
                    _logger.LogInformation($"No portfolios found containing stock {priceEvent.StockId}");
                    return;
                }

                // 2. 獲取所有相關的股票價格
                var allStockIds = portfolios
                    .SelectMany(p => p.Stocks.Select(s => s.StockId))
                    .Distinct()
                    .ToList();

                var stocksFilter = Builders<Stock>.Filter.In(s => s.Id, allStockIds);
                var stocks = await _mongoDbService.Stocks
                    .Find(stocksFilter)
                    .ToListAsync();

                var stockPrices = stocks.ToDictionary(s => s.Id, s => s.Price);

                // 3. 更新每個投資組合
                foreach (var portfolio in portfolios)
                {
                    await _calculationService.UpdatePortfolioValues(portfolio, stockPrices);
                }

                _logger.LogInformation($"Completed portfolio updates for {portfolios.Count} portfolios");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating portfolios for stock {priceEvent.StockId}");
                throw;
            }
        }

        public async Task<List<string>> GetAffectedPortfolios(string stockId)
        {
            try
            {
                var filter = Builders<Portfolio>.Filter.ElemMatch(
                    p => p.Stocks,
                    stock => stock.StockId == stockId
                );

                var portfolios = await _mongoDbService.Portfolios
                    .Find(filter)
                    .Project(p => p.Id)
                    .ToListAsync();

                _logger.LogInformation($"Found {portfolios.Count} portfolios containing stock {stockId}");
                return portfolios;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting affected portfolios for stock {stockId}");
                throw;
            }
        }
    }
}