using System;

namespace SalonSuite.Models;

/// <summary>
/// 1. Customer Management & CRM Entity (Maps to SQL Server Customers table)
/// </summary>
public class CustomerRecord
{
    public int Id { get; set; }
    public string ClientCode => $"#CUS-{Id:D3}";
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int LoyaltyPoints { get; set; } = 0;
    public string Tier { get; set; } = "Bronze"; // Bronze, Silver, Gold, Platinum
    public decimal TotalSpent { get; set; } = 0;
    public int VisitsCount { get; set; } = 0;
    public DateTime? LastVisit { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string TierBadgeColor => Tier switch
    {
        "Platinum" => "#8B5CF6", // Purple
        "Gold" => "#D97706",     // Gold/Amber
        "Silver" => "#64748B",   // Slate/Silver
        _ => "#B45309"           // Bronze
    };
}
