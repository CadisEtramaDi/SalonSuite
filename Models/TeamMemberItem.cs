using System;

namespace SalonSuite.Models;

/// <summary>
/// 4. Employee & Staff Management Entity (Maps to SQL Server Employees table)
/// </summary>
public class TeamMemberItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = "Senior Stylist";
    public string Specialization { get; set; } = "Precision Haircut & Styling";
    public decimal CommissionRate { get; set; } = 0.25m; // 25% default
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
