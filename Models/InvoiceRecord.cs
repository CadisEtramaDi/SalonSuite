using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

/// <summary>
/// 7. Billing & Invoicing Entity (Maps to Firebase Firestore 'invoices' collection)
/// </summary>
[FirestoreData]
public class InvoiceRecord
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public int AppointmentId { get; set; }

    [FirestoreProperty]
    public int? CustomerId { get; set; }

    public string InvoiceNumber => $"INV-{DateTime.Today.Year}-{Id:D4}";

    [FirestoreProperty]
    public string ClientName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string ServiceName { get; set; } = string.Empty;

    [FirestoreProperty]
    public string StylistName { get; set; } = string.Empty;

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal Subtotal { get; set; }

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal RetailAddonsTotal { get; set; } = 0;

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal Discount { get; set; }

    [FirestoreProperty]
    public string? PromoCode { get; set; }

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal PromoDiscount { get; set; } = 0;

    [FirestoreProperty]
    public int LoyaltyPointsRedeemed { get; set; } = 0;

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal LoyaltyDiscount { get; set; } = 0;

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal Total { get; set; }

    [FirestoreProperty(ConverterType = typeof(SalonSuite.Services.FirestoreDecimalConverter))]
    public decimal AmountPaid { get; set; }

    public decimal Change => AmountPaid >= Total ? AmountPaid - Total : 0;

    [FirestoreProperty]
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Credit Card, GCash, Maya

    [FirestoreProperty]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [FirestoreProperty]
    public string CashierName { get; set; } = "Front Desk";

    public int PointsEarned => (int)(Total / 100); // 1 point per ₱100 spend

    [FirestoreProperty]
    public string? ReceiptImageUrl { get; set; }
}
