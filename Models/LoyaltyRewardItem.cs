using System;

namespace SalonSuite.Models;

/// <summary>
/// 8. Customer Loyalty CRM Rewards Entity (Maps to SQL Server LoyaltyRewards table)
/// </summary>
public class LoyaltyRewardItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int PointsRequired { get; set; }
    public decimal DiscountValue { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
