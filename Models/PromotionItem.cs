using System;

namespace SalonSuite.Models;

/// <summary>
/// 9. Promotions & Vouchers Entity (Maps to SQL Server Promotions table)
/// </summary>
public class PromotionItem
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage"; // Percentage or Fixed
    public decimal DiscountValue { get; set; }
    public decimal MinSpend { get; set; } = 0;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);
    public bool IsActive { get; set; } = true;
    public int UsageCount { get; set; } = 0;

    public bool IsValidNow => IsActive && DateTime.Today >= StartDate && DateTime.Today <= EndDate;
}
