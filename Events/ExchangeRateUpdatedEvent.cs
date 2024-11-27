using MediatR;

namespace PortfolioManager.Events;

public class ExchangeRateUpdatedEvent : INotification
{
    public decimal OldRate { get; set; }
    public decimal NewRate { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; }
    public string Source { get; set; } // 例如: "Manual", "API", "System"
}