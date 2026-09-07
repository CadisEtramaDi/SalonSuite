using System;

namespace SalonSuite.Models;

/// <summary>
/// 3. Service Management Entity (Maps to SQL Server Services table)
/// </summary>
public class ServiceOfferItem
{
    public int Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Hair"; // Hair, Treatment, Color, Styling, Spa
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; } = 45;
    public bool IsActive { get; set; } = true;
    public string ImageUrl { get; set; } = string.Empty;
    public string PriceFormatted => $"From ₱{Price:N0}";
}
