using System;
using System.Collections.Generic;

namespace SalonSuite.Models;

public class ServiceOfferItem
{
    public string Number { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PriceFormatted => $"From ₱{Price:N0}";
}

public class TeamMemberItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
}

public class ServicePackageItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PriceFormatted => $"₱{Price:N0}";
    public bool IsPopular { get; set; }
    public List<string> Features { get; set; } = new();
}

public class TestimonialItem
{
    public int Id { get; set; }
    public string Quote { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
}

public class AppointmentRecord
{
    public int Id { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string TimeSlot { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Confirmed"; // Confirmed, In Progress, Completed, Cancelled
    public bool IsPaid { get; set; } = false;
    public string? Notes { get; set; }
}

public class InvoiceRecord
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public string InvoiceNumber => $"INV-{DateTime.Today.Year}-{Id:D4}";
    public string ClientName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal Change => AmountPaid >= Total ? AmountPaid - Total : 0;
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Credit Card, GCash, Maya
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string CashierName { get; set; } = "Front Desk";
}

public class UserSession
{
    public bool IsLoggedIn { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
