namespace PortfolioManager.Models;

public class PortfolioDailyValueResponse
{
    public string? PortfolioId { get; set; }
    public List<DailyValueData>? Values { get; set; }
    public ValueSummary? Summary { get; set; }
}