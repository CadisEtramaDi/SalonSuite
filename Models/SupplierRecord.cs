using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 6. Supplier Management Entity (Maps to Firebase Firestore 'suppliers' collection)
/// </summary>
[FirestoreData]
public class SupplierRecord
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string CompanyName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ContactPerson { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Email { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Phone { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Address { get; set; } = string.Empty;

    [FirestoreProperty]
    public string SuppliedCategory { get; set; } = "Hair Care & Cosmetics";

    [FirestoreProperty]
    public string PaymentTerms { get; set; } = "Net 30";

    [FirestoreProperty]
    public bool IsActive { get; set; } = true;
}
