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

        // 2. Auto-assign free specialist if unspecified or 'Any Available'
        if (string.IsNullOrWhiteSpace(appointment.StylistName) ||
            appointment.StylistName.Contains("Any", StringComparison.OrdinalIgnoreCase) ||
            appointment.StylistName.Contains("No Preference", StringComparison.OrdinalIgnoreCase))
        {
            appointment.StylistName = FindAvailableStylist(appointment.Date, appointment.TimeSlot);
        }

        // 3. Persist Appointment to Firestore
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

    /// <summary>
    /// Finds an available specialist for the specified date and time slot.
    /// If preferredStylist is explicitly provided and matches an active stylist, returns that stylist.
    /// If preferredStylist is null/empty or "Any Available Specialist", assigns the free stylist
    /// with the fewest bookings on that date (fair workload distribution).
    /// </summary>
    public string FindAvailableStylist(DateTime date, string? timeSlot, string? preferredStylist = null)
    {
        var activeStylists = Stylists.Where(s => s.IsActive).ToList();
        if (activeStylists.Count == 0)
        {
            return "Sofia Martinez";
        }

        // 1. If customer requested a specific stylist, respect preference
        if (!string.IsNullOrWhiteSpace(preferredStylist) &&
            !preferredStylist.Contains("Any", StringComparison.OrdinalIgnoreCase) &&
            !preferredStylist.Contains("No Preference", StringComparison.OrdinalIgnoreCase) &&
            !preferredStylist.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            var matched = activeStylists.FirstOrDefault(s => s.Name.Equals(preferredStylist.Trim(), StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                return matched.Name;
            }
        }

        // 2. Identify busy stylists for this specific date and time slot
        var busyStylistNames = Appointments
            .Where(a => a.Date.Date == date.Date &&
                        !string.IsNullOrWhiteSpace(timeSlot) &&
                        string.Equals(a.TimeSlot, timeSlot, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(a.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.StylistName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 3. Stylists who are free at this exact slot
        var freeStylists = activeStylists
            .Where(s => !busyStylistNames.Contains(s.Name))
            .ToList();

        // 4. Candidate pool: free stylists if available, otherwise all active stylists
        var candidates = freeStylists.Count > 0 ? freeStylists : activeStylists;

        // 5. Load balancing: pick candidate with the lowest appointment volume on this day
        var stylistDailyCounts = Appointments
            .Where(a => a.Date.Date == date.Date && !string.Equals(a.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .GroupBy(a => a.StylistName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var bestStylist = candidates
            .OrderBy(s => stylistDailyCounts.TryGetValue(s.Name, out var count) ? count : 0)
            .ThenBy(s => s.Id)
            .First();

        return bestStylist.Name;
    }

    /// <summary>
    /// Checks if a stylist is currently booked for a specific date and time slot.
    /// </summary>
    public bool IsStylistBusy(string stylistName, DateTime date, string? timeSlot)
    {
        if (string.IsNullOrWhiteSpace(stylistName) || string.IsNullOrWhiteSpace(timeSlot)) return false;
        return Appointments.Any(a =>
            a.Date.Date == date.Date &&
            string.Equals(a.TimeSlot, timeSlot, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.StylistName, stylistName, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a.Status, "Cancelled", StringComparison.OrdinalIgnoreCase));
    }
}
