using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 3. Service Management Entity (Maps to Firebase Firestore 'services' collection)
/// </summary>
[FirestoreData]
public class ServiceOfferItem
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string Number { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Category { get; set; } = "Hair"; // Hair, Treatment, Color, Styling, Spa

    [FirestoreProperty]
    public string Description { get; set; } = string.Empty;

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal Price { get; set; }

    [FirestoreProperty]
    public int DurationMinutes { get; set; } = 45;

    [FirestoreProperty]
    public bool IsActive { get; set; } = true;

    [FirestoreProperty]
    public string ImageUrl { get; set; } = string.Empty; // Cloudinary URL

    public string PriceFormatted => $"From ₱{Price:N0}";
}
