using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 1. Customer Management & CRM Entity (Maps to Firebase Firestore 'customers' collection)
/// </summary>
[FirestoreData]
public class CustomerRecord
{
    [FirestoreProperty]
    public int Id { get; set; }

    public string ClientCode => $"#CUS-{Id:D3}";

    [FirestoreProperty]
    public string FullName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Phone { get; set; } = string.Empty;

    [FirestoreProperty]
    public int LoyaltyPoints { get; set; } = 0;

    [FirestoreProperty]
    public string Tier { get; set; } = "Bronze"; // Bronze, Silver, Gold, Platinum

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal TotalSpent { get; set; } = 0;

    [FirestoreProperty]
    public int VisitsCount { get; set; } = 0;

    [FirestoreProperty]
    public DateTime? LastVisit { get; set; }

    [FirestoreProperty]
    public string? Notes { get; set; }

    [FirestoreProperty]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string TierBadgeColor => Tier switch
    {
        "Platinum" => "#7C3AED", // Vibrant Purple
        "Gold" => "#D97706",     // Amber / Gold
        "Silver" => "#4B5563",   // Slate / Silver
        _ => "#9A3412"           // Deep Bronze
    };
}
