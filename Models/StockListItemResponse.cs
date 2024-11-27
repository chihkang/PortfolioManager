using System.Diagnostics.CodeAnalysis;
using PortfolioManager.Attribute;

namespace PortfolioManager.Models;

[Dto]
[SuppressMessage("Rider", "UnusedAutoPropertyAccessor.Global")]
public class StockListItemResponse
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Alias { get; set; }
}