using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class ClientPortal : ComponentBase, IDisposable
{
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;

    private string searchPhoneInput = "";
    private string lookupMessage = "";
    private CustomerRecord? activeCustomer;

    protected override void OnInitialized()
    {
        SalonService.OnChange += HandleDataChanged;
        SalonService.EnsureSeedData();

        // 1. If customer is currently logged in, auto-load their profile
        if (SalonService.CurrentUser.IsLoggedIn)
        {
            activeCustomer = SalonService.FindCustomerByPhoneOrName(SalonService.CurrentUser.Email) ??
                             SalonService.FindCustomerByPhoneOrName(SalonService.CurrentUser.Name);
            if (activeCustomer != null)
            {
                lookupMessage = $"Welcome back, {activeCustomer.FullName}! Your loyalty rewards are active.";
            }
        }
        
        // 2. If no logged in user, default to the first VIP customer as demonstration
        if (activeCustomer == null && SalonService.Customers.Any())
        {
            activeCustomer = SalonService.Customers.FirstOrDefault(c => c.Tier == "Platinum") ?? 
                             SalonService.Customers.FirstOrDefault(c => c.Tier == "Gold") ?? 
                             SalonService.Customers.First();
            lookupMessage = $"Showing VIP profile for {activeCustomer.FullName}. Enter your phone or name above to search.";
        }
    }

    private void HandleDataChanged()
    {
        if (activeCustomer != null)
        {
            activeCustomer = SalonService.Customers.FirstOrDefault(c => c.Id == activeCustomer.Id) ?? activeCustomer;
        }
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        SalonService.OnChange -= HandleDataChanged;
    }

    private void HandleSearchKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            LookupCustomer();
        }
    }

    private void LookupCustomer()
    {
        if (string.IsNullOrWhiteSpace(searchPhoneInput))
        {
            lookupMessage = "Please enter your mobile phone number, name, or client code.";
            return;
        }

        SalonService.EnsureSeedData();
        var found = SalonService.FindCustomerByPhoneOrName(searchPhoneInput.Trim());
        if (found != null)
        {
            activeCustomer = found;
            lookupMessage = $"Found loyalty profile for {activeCustomer.FullName} ({activeCustomer.ClientCode})!";
        }
        else
        {
            lookupMessage = $"No loyalty profile found matching '{searchPhoneInput}'. Book an appointment or register to start earning rewards!";
        }
    }

    private IEnumerable<AppointmentRecord> CustomerAppointments =>
        activeCustomer != null
            ? SalonService.Appointments.Where(a => (a.CustomerId.HasValue && a.CustomerId.Value == activeCustomer.Id) ||
                                                   (!string.IsNullOrEmpty(a.ClientPhone) && !string.IsNullOrEmpty(activeCustomer.Phone) && SalonDataService.NormalizePhoneNumber(a.ClientPhone) == SalonDataService.NormalizePhoneNumber(activeCustomer.Phone)) ||
                                                   a.ClientName.Equals(activeCustomer.FullName, StringComparison.OrdinalIgnoreCase))
                                       .OrderByDescending(a => a.Date)
            : Enumerable.Empty<AppointmentRecord>();

    private IEnumerable<InvoiceRecord> CustomerInvoices =>
        activeCustomer != null
            ? SalonService.Invoices.Where(i => (i.CustomerId.HasValue && i.CustomerId.Value == activeCustomer.Id) ||
                                               i.ClientName.Equals(activeCustomer.FullName, StringComparison.OrdinalIgnoreCase))
                                   .OrderByDescending(i => i.Timestamp)
            : Enumerable.Empty<InvoiceRecord>();

    private (string NextTier, decimal TargetSpend, int ProgressPercent) GetTierProgress(decimal totalSpent)
    {
        if (totalSpent < 3500)
        {
            int pct = (int)((totalSpent / 3500m) * 100);
            return ("Silver", 3500, Math.Min(100, Math.Max(5, pct)));
        }
        if (totalSpent < 10000)
        {
            decimal range = 10000 - 3500;
            decimal current = totalSpent - 3500;
            int pct = (int)((current / range) * 100);
            return ("Gold", 10000, Math.Min(100, Math.Max(5, pct)));
        }
        if (totalSpent < 25000)
        {
            decimal range = 25000 - 10000;
            decimal current = totalSpent - 10000;
            int pct = (int)((current / range) * 100);
            return ("Platinum", 25000, Math.Min(100, Math.Max(5, pct)));
        }
        return ("", 25000, 100);
    }

    private void HandleLogout()
    {
        SalonService.Logout();
        Navigation.NavigateTo("/login");
    }
}
