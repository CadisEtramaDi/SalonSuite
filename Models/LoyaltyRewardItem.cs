using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 8. Customer Loyalty CRM Rewards Entity (Maps to Firebase Firestore 'loyaltyRewards' collection)
/// </summary>
[FirestoreData]
public class LoyaltyRewardItem
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string Title { get; set; } = string.Empty;

    [FirestoreProperty]
    public int PointsRequired { get; set; }

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal DiscountValue { get; set; }

    [FirestoreProperty]
    public string Description { get; set; } = string.Empty;

    [FirestoreProperty]
    public bool IsActive { get; set; } = true;
}
