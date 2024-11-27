namespace PortfolioManager.Models;

public class EnrichedPortfolioStock
{
    public required string? StockId { get; set; }
    public decimal Quantity { get; set; }
    public Stock? StockDetails { get; set; }
}