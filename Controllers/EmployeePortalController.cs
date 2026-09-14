using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class EmployeePortal : ComponentBase
{
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    private string currentTab = "schedule";
    private bool filterOnlyMe = true;
    private bool showWalkinModal = false;
    private string currentStylistName = "Sofia Martinez";

    private AppointmentRecord newChairAppt = new()
    {
        ServiceName = "01 Haircut",
        TimeSlot = "Now (Seated)",
        Price = 850,
        Status = "In Progress"
    };

    protected override void OnInitialized()
    {
        if (SalonService.CurrentUser.IsLoggedIn && !string.IsNullOrWhiteSpace(SalonService.CurrentUser.Name))
        {
            currentStylistName = SalonService.CurrentUser.Name;
        }
        else
        {
            currentStylistName = SalonService.Stylists.FirstOrDefault()?.Name ?? "Sofia Martinez";
        }
    }

    private List<AppointmentRecord> MyAppointments =>
        SalonService.Appointments.Where(a => a.StylistName.Contains(currentStylistName, StringComparison.OrdinalIgnoreCase) ||
                                            currentStylistName.Contains(a.StylistName, StringComparison.OrdinalIgnoreCase))
                                 .OrderByDescending(a => a.Id)
                                 .ToList();

    private List<AppointmentRecord> DisplayedAppointments =>
        (filterOnlyMe ? MyAppointments : SalonService.Appointments)
        .OrderByDescending(a => a.Id)
        .ToList();

    private void SaveNote(int id, string? notes)
    {
        if (notes != null)
        {
            SalonService.UpdateAppointmentNotes(id, notes);
        }
    }

    private void SaveChairBooking()
    {
        if (string.IsNullOrWhiteSpace(newChairAppt.ClientName)) return;

        decimal price = 850;
        var pkg = SalonService.Packages.FirstOrDefault(p => newChairAppt.ServiceName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (pkg != null) price = pkg.Price;
        var svc = SalonService.Services.FirstOrDefault(s => newChairAppt.ServiceName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (svc != null) price = svc.Price;

        newChairAppt.StylistName = currentStylistName;
        newChairAppt.Price = price;
        newChairAppt.Date = DateTime.Today;

        SalonService.AddAppointment(newChairAppt);
        showWalkinModal = false;
        newChairAppt = new()
        {
            ServiceName = "01 Haircut",
            TimeSlot = "Now (Seated)",
            Price = 850,
            Status = "In Progress"
        };
    }

    private void HandleLogout()
    {
        SalonService.Logout();
        Navigation.NavigateTo("/login");
    }
}
