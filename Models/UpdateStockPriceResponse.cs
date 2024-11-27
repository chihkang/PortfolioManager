using System.Diagnostics.CodeAnalysis;
using PortfolioManager.Attribute;

namespace PortfolioManager.Models;

[Dto]
[SuppressMessage("Rider", "UnusedAutoPropertyAccessor.Global")]
public class UpdateStockPriceResponse
{
    public string? Name { get; set; }
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public required string? Currency { get; set; }
    public DateTime LastUpdated { get; set; }
}