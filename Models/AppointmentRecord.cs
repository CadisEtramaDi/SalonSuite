using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 2. Appointment Scheduling Entity (Maps to Firebase Firestore 'appointments' collection)
/// </summary>
[FirestoreData]
public class AppointmentRecord
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public int? CustomerId { get; set; }

    [FirestoreProperty]
    public string ClientName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ClientPhone { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ClientEmail { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ServiceName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string StylistName { get; set; } = string.Empty;

    [FirestoreProperty]
    public DateTime Date { get; set; } = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc);

    [FirestoreProperty]
    public string TimeSlot { get; set; } = string.Empty;

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal Price { get; set; }

    [FirestoreProperty]
    public string Status { get; set; } = "Confirmed"; // Confirmed, In Progress, Completed, Cancelled

    [FirestoreProperty]
    public bool IsPaid { get; set; } = false;

    [FirestoreProperty]
    public string? Notes { get; set; }
}
