using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 4. Employee & Staff Management Entity (Maps to Firebase Firestore 'employees' collection)
/// </summary>
[FirestoreData]
public class TeamMemberItem
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string Name { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Role { get; set; } = "Senior Stylist";

    [FirestoreProperty]
    public string Specialization { get; set; } = "Precision Haircut & Styling";

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal CommissionRate { get; set; } = 0.25m; // 25% default

    [FirestoreProperty]
    public string Phone { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ImageUrl { get; set; } = string.Empty; // Cloudinary URL

    [FirestoreProperty]
    public bool IsActive { get; set; } = true;
}
