namespace PortfolioManager.Models;

public class EnrichedPortfolioResponse
{
    public required string? Id { get; set; }
    public DateTime LastUpdated { get; set; }
    public required List<EnrichedPortfolioStock> Stocks { get; set; }
    public required string? UserId { get; set; }
}