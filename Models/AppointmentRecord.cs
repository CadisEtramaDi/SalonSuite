using System;

namespace SalonSuite.Models;

/// <summary>
/// 2. Appointment Scheduling Entity (Maps to SQL Server Appointments table)
/// </summary>
public class AppointmentRecord
{
    public int Id { get; set; }
    public int? CustomerId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string ClientPhone { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string StylistName { get; set; } = string.Empty;
    public DateTime Date { get; set; } = DateTime.Today;
    public string TimeSlot { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Status { get; set; } = "Confirmed"; // Confirmed, In Progress, Completed, Cancelled
    public bool IsPaid { get; set; } = false;
    public string? Notes { get; set; }
}
