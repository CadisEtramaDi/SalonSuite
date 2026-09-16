using System;
using System.Collections.Generic;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

[FirestoreData]
public class ServicePackageItem
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Description { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ImageUrl { get; set; } = string.Empty; // Cloudinary URL

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal Price { get; set; }

    [FirestoreProperty]
    public int DurationMinutes { get; set; } = 90;

    public string PriceFormatted => $"₱{Price:N0}";

    [FirestoreProperty]
    public bool IsPopular { get; set; }

    [FirestoreProperty]
    public List<string> Features { get; set; } = new();
}
