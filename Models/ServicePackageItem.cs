using System;
using System.Collections.Generic;

namespace SalonSuite.Models;

public class ServicePackageItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; } = 90;
    public string PriceFormatted => $"₱{Price:N0}";
    public bool IsPopular { get; set; }
    public List<string> Features { get; set; } = new();
}
