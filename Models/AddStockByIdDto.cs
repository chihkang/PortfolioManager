namespace PortfolioManager.Models;

public class AddStockByIdDto
{
    public required string StockId { get; set; }
    public decimal Quantity { get; set; }
}