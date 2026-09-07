using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<AppointmentRecord> Appointments { get; private set; } = new();

    private void LoadAppointmentsFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, CustomerId, ClientName, ClientPhone, ClientEmail, ServiceName, StylistName, Date, TimeSlot, Price, Status, IsPaid, Notes FROM dbo.Appointments ORDER BY Id DESC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<AppointmentRecord>();
            while (reader.Read())
            {
                list.Add(new AppointmentRecord
                {
                    Id = reader.GetInt32(0),
                    CustomerId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
                    ClientName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    ClientPhone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ClientEmail = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ServiceName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    StylistName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Date = reader.IsDBNull(7) ? DateTime.Today : reader.GetDateTime(7),
                    TimeSlot = reader.IsDBNull(8) ? "" : reader.GetString(8),
                    Price = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9),
                    Status = reader.IsDBNull(10) ? "Confirmed" : reader.GetString(10),
                    IsPaid = !reader.IsDBNull(11) && reader.GetBoolean(11),
                    Notes = reader.IsDBNull(12) ? null : reader.GetString(12)
                });
            }
            if (list.Count > 0) Appointments = list;
        }
        catch { }
    }

    public void AddAppointment(AppointmentRecord appointment)
    {
        // 1. Auto-register or link client in CRM
        if (!string.IsNullOrWhiteSpace(appointment.ClientName))
        {
            var customer = EnsureCustomer(
                appointment.ClientName,
                appointment.ClientPhone,
                appointment.ClientEmail,
                appointment.Notes
            );
            appointment.CustomerId = customer.Id;
        }

        // 2. Persist Appointment to SQL Server database
        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.Appointments (CustomerId, ClientName, ClientPhone, ClientEmail, ServiceName, StylistName, Date, TimeSlot, Price, Status, IsPaid, Notes) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@CustomerId, @ClientName, @ClientPhone, @ClientEmail, @ServiceName, @StylistName, @Date, @TimeSlot, @Price, @Status, @IsPaid, @Notes);",
            new SqlParameter("@CustomerId", (object?)appointment.CustomerId ?? DBNull.Value),
            new SqlParameter("@ClientName", appointment.ClientName),
            new SqlParameter("@ClientPhone", appointment.ClientPhone),
            new SqlParameter("@ClientEmail", (object?)appointment.ClientEmail ?? DBNull.Value),
            new SqlParameter("@ServiceName", appointment.ServiceName),
            new SqlParameter("@StylistName", appointment.StylistName),
            new SqlParameter("@Date", appointment.Date.Date),
            new SqlParameter("@TimeSlot", appointment.TimeSlot),
            new SqlParameter("@Price", appointment.Price),
            new SqlParameter("@Status", appointment.Status),
            new SqlParameter("@IsPaid", appointment.IsPaid),
            new SqlParameter("@Notes", (object?)appointment.Notes ?? DBNull.Value)
        );

        appointment.Id = dbId > 0 ? dbId : (Appointments.Count > 0 ? Appointments.Max(a => a.Id) + 1 : 1);
        Appointments.Add(appointment);
        NotifyStateChanged();
    }

    public void UpdateAppointmentStatus(int id, string newStatus)
    {
        var appt = Appointments.FirstOrDefault(a => a.Id == id);
        if (appt != null)
        {
            appt.Status = newStatus;
            ExecuteSqlNonQuery("UPDATE dbo.Appointments SET Status = @Status WHERE Id = @Id;",
                new SqlParameter("@Id", id),
                new SqlParameter("@Status", newStatus));
            NotifyStateChanged();
        }
    }

    public void UpdateAppointmentNotes(int id, string notes)
    {
        var appt = Appointments.FirstOrDefault(a => a.Id == id);
        if (appt != null)
        {
            appt.Notes = notes;
            ExecuteSqlNonQuery("UPDATE dbo.Appointments SET Notes = @Notes WHERE Id = @Id;",
                new SqlParameter("@Id", id),
                new SqlParameter("@Notes", notes));
            NotifyStateChanged();
        }
    }

    public void DeleteAppointment(int id)
    {
        Appointments.RemoveAll(a => a.Id == id);
        ExecuteSqlNonQuery("DELETE FROM dbo.Appointments WHERE Id = @Id;", new SqlParameter("@Id", id));
        NotifyStateChanged();
    }

    public static int ParseTimeSlotToMinutes(string? timeSlot)
    {
        if (string.IsNullOrWhiteSpace(timeSlot)) return 0;
        var trimmed = timeSlot.Trim();
        if (DateTime.TryParse(trimmed, out var dt))
        {
            return dt.Hour * 60 + dt.Minute;
        }
        return 0;
    }
}
