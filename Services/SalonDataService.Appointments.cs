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

    private async Task LoadAppointmentsFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("appointments").GetSnapshotAsync();
            var list = new List<AppointmentRecord>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<AppointmentRecord>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Appointments = list.OrderByDescending(a => a.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading appointments: {ex.Message}");
        }
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

        // 2. Persist Appointment to Firestore
        appointment.Id = Appointments.Count > 0 ? Appointments.Max(a => a.Id) + 1 : 1;
        if (appointment.Date.Kind != DateTimeKind.Utc)
        {
            appointment.Date = DateTime.SpecifyKind(appointment.Date, DateTimeKind.Utc);
        }
        Appointments.Insert(0, appointment);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("appointments", appointment.Id.ToString(), appointment));
    }

    public void UpdateAppointmentStatus(int id, string newStatus)
    {
        var appt = Appointments.FirstOrDefault(a => a.Id == id);
        if (appt != null)
        {
            appt.Status = newStatus;
            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("appointments", appt.Id.ToString(), appt));
        }
    }

    public void UpdateAppointmentNotes(int id, string notes)
    {
        var appt = Appointments.FirstOrDefault(a => a.Id == id);
        if (appt != null)
        {
            appt.Notes = notes;
            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("appointments", appt.Id.ToString(), appt));
        }
    }

    public void ReassignAppointmentStylist(int id, string newStylist)
    {
        var appt = Appointments.FirstOrDefault(a => a.Id == id);
        if (appt != null && !string.IsNullOrWhiteSpace(newStylist))
        {
            appt.StylistName = newStylist;
            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("appointments", appt.Id.ToString(), appt));
        }
    }

    public void DeleteAppointment(int id)
    {
        Appointments.RemoveAll(a => a.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("appointments", id.ToString()));
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
