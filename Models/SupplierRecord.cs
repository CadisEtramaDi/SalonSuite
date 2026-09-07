using System;

namespace SalonSuite.Models;

/// <summary>
/// 6. Supplier Management Entity (Maps to SQL Server Suppliers table)
/// </summary>
public class SupplierRecord
{
    public int Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string SuppliedCategory { get; set; } = "Hair Care & Cosmetics";
    public string PaymentTerms { get; set; } = "Net 30";
    public bool IsActive { get; set; } = true;
}
