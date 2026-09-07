using System;

namespace SalonSuite.Models;

/// <summary>
/// 7. Billing & Invoicing Entity (Maps to SQL Server Invoices table)
/// </summary>
public class InvoiceRecord
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int? CustomerId { get; set; }
    public string InvoiceNumber => $"INV-{DateTime.Today.Year}-{Id:D4}";
    public string ClientName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal RetailAddonsTotal { get; set; } = 0;
    public decimal Discount { get; set; }
    public string? PromoCode { get; set; }
    public decimal PromoDiscount { get; set; } = 0;
    public int LoyaltyPointsRedeemed { get; set; } = 0;
    public decimal LoyaltyDiscount { get; set; } = 0;
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Change => AmountPaid >= Total ? AmountPaid - Total : 0;
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Credit Card, GCash, Maya
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string CashierName { get; set; } = "Front Desk";
    public int PointsEarned => (int)(Total / 100); // 1 point per ₱100 spend
}
