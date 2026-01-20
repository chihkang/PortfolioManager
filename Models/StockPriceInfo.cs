using System.Diagnostics.CodeAnalysis;
using PortfolioManager.Attribute;

namespace PortfolioManager.Models;

[Dto]
[SuppressMessage("Rider", "UnusedAutoPropertyAccessor.Global")]
public class StockPriceInfo
{
    public string? Id { get; set; }
    public decimal Price { get; set; }
    public string? Currency { get; set; }
    public DateTime? LastUpdated { get; set; }
}