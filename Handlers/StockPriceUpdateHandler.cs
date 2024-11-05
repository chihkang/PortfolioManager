using MediatR;
using PortfolioManager.Events;
using PortfolioManager.Services;
using Microsoft.Extensions.Options;
using PortfolioManager.Configuration;

namespace PortfolioManager.Handlers
{
    public class StockPriceUpdateHandler : INotificationHandler<StockPriceUpdatedEvent>
    {
        private readonly PortfolioUpdateService _portfolioUpdateService;
        private readonly PortfolioCacheService _cacheService;
        private readonly ILogger<StockPriceUpdateHandler> _logger;
        private readonly IOptions<PortfolioUpdateOptions> _options;

        public StockPriceUpdateHandler(
            PortfolioUpdateService portfolioUpdateService,
            PortfolioCacheService cacheService,
            ILogger<StockPriceUpdateHandler> logger,
            IOptions<PortfolioUpdateOptions> options)
        {
            _portfolioUpdateService = portfolioUpdateService;
            _cacheService = cacheService;
            _logger = logger;
            _options = options;
        }

        public async Task Handle(StockPriceUpdatedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation($"Handling stock price update for stock {notification.StockId}: " +
                    $"Old price: {notification.OldPrice}, New price: {notification.NewPrice}");

                // 更新相關投資組合
                await _portfolioUpdateService.HandleStockPriceUpdated(notification);

                // 清除相關快取
                var affectedPortfolios = await _portfolioUpdateService
                    .GetAffectedPortfolios(notification.StockId);

                foreach (var portfolioId in affectedPortfolios)
                {
                    await _cacheService.InvalidatePortfolioCache(portfolioId);
                    _logger.LogInformation($"Invalidated cache for portfolio {portfolioId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    $"Error handling stock price update for stock {notification.StockId}");
                
                // 執行重試邏輯
                await RetryUpdateIfNeeded(notification, ex, cancellationToken);
            }
        }

        private async Task RetryUpdateIfNeeded(
            StockPriceUpdatedEvent notification, 
            Exception originalException,
            CancellationToken cancellationToken)
        {
            var retryCount = 0;
            var maxRetries = _options.Value.MaxRetryAttempts;

            while (retryCount < maxRetries)
            {
                try
                {
                    // 指數退避
                    var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                    await Task.Delay(delay, cancellationToken);

                    _logger.LogInformation(
                        $"Retrying stock price update for stock {notification.StockId}. " +
                        $"Attempt {retryCount + 1} of {maxRetries}");

                    await Handle(notification, cancellationToken);
                    return; // 如果成功，提前返回
                }
                catch (Exception retryEx)
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError(retryEx, 
                            $"Final retry attempt failed for stock {notification.StockId}. " +
                            $"Original error: {originalException.Message}");
                        throw; // 重試全部失敗，拋出最後的異常
                    }
                }
            }
        }
    }
}