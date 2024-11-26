namespace PortfolioManager.Models;

public class ValueSummary
{
    public decimal HighestValue { get; set; }
    public DateTime HighestValueDate { get; set; }
    public decimal LowestValue { get; set; }
    public DateTime LowestValueDate { get; set; }
    public decimal StartValue { get; set; }
    public decimal EndValue { get; set; }
    public decimal ChangePercentage { get; set; }
}