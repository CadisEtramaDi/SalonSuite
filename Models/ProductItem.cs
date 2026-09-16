using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 5. Product Inventory Entity (Maps to Firebase Firestore 'products' collection)
/// </summary>
[FirestoreData]
public class ProductItem
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string SKU { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Category { get; set; } = "Hair Care"; // Hair Care, Styling, Color, Equipment, Retail

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal CostPrice { get; set; }

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal RetailPrice { get; set; }

    [FirestoreProperty]
    public int StockQuantity { get; set; }

    [FirestoreProperty]
    public int ReorderLevel { get; set; } = 5;

    [FirestoreProperty]
    public int? SupplierId { get; set; }

    [FirestoreProperty]
    public string SupplierName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Unit { get; set; } = "Bottle"; // Bottle, Tube, Jar, Piece, Box

    [FirestoreProperty]
    public string ImageUrl { get; set; } = string.Empty; // Cloudinary URL

    public bool IsLowStock => StockQuantity <= ReorderLevel;
}
