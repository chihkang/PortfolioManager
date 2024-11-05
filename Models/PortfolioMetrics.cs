namespace PortfolioManager.Models;
public class PortfolioMetrics
{
    public string PortfolioId { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime LastUpdated { get; set; }
    public int NumberOfStocks { get; set; }
    public decimal AverageStockValue { get; set; }
    public List<StockMetric> StockMetrics { get; set; }
}

public class StockMetric
{
    public string StockId { get; set; }
    public string StockName { get; set; }
    public decimal Quantity { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal PercentageOfPortfolio { get; set; }
    public string Currency { get; set; }
}

