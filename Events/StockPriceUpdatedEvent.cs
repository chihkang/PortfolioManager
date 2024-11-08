namespace PortfolioManager.Events;
using MediatR;
public class StockPriceUpdatedEvent:INotification
{
    public string StockId { get; set; }
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime UpdatedAt { get; set; }
}