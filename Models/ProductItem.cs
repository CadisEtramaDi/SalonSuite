using System;

namespace SalonSuite.Models;

/// <summary>
/// 5. Product Inventory Entity (Maps to SQL Server Products table)
/// </summary>
public class ProductItem
{
    public int Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Hair Care"; // Hair Care, Styling, Color, Equipment, Retail
    public decimal CostPrice { get; set; }
    public decimal RetailPrice { get; set; }
    public int StockQuantity { get; set; }
    public int ReorderLevel { get; set; } = 5;
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string Unit { get; set; } = "Bottle"; // Bottle, Tube, Jar, Piece, Box
    public bool IsLowStock => StockQuantity <= ReorderLevel;
}
