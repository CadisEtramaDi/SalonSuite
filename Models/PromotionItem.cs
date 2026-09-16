using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 9. Promotions & Vouchers Entity (Maps to Firebase Firestore 'promotions' collection)
/// </summary>
[FirestoreData]
public class PromotionItem
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string Code { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Title { get; set; } = string.Empty;

    [FirestoreProperty]
    public string DiscountType { get; set; } = "Percentage"; // Percentage or Fixed

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal DiscountValue { get; set; }

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal MinSpend { get; set; } = 0;

    [FirestoreProperty]
    public DateTime StartDate { get; set; } = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

    [FirestoreProperty]
    public DateTime EndDate { get; set; } = DateTime.SpecifyKind(DateTime.Today.AddMonths(1), DateTimeKind.Utc);

    [FirestoreProperty]
    public bool IsActive { get; set; } = true;

    [FirestoreProperty]
    public int UsageCount { get; set; } = 0;

    public bool IsValidNow => IsActive && DateTime.Today >= StartDate && DateTime.Today <= EndDate;
}
