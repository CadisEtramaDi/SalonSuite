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
    [Inject] public EmailReceiptService EmailService { get; set; } = default!;

    [SupplyParameterFromQuery] public string? phone { get; set; }
    [SupplyParameterFromQuery] public string? email { get; set; }
    [SupplyParameterFromQuery] public string? appt_id { get; set; }
    [SupplyParameterFromQuery] public string? query { get; set; }

    private string searchPhoneInput = "";
    private string lookupMessage = "";
    private CustomerRecord? activeCustomer;

    private AppointmentRecord? apptToCancel = null;
    private string cancelReason = "Change of schedule";
    private bool isCancellingAppt = false;
    private string? portalActionFeedback = null;
    private bool isPortalFeedbackSuccess = true;
    private bool isMobileNavOpen = false;

    protected override void OnInitialized()
    {
        SalonService.OnChange += HandleDataChanged;
        SalonService.EnsureSeedData();

        // 1. Direct query parameter lookup (e.g. ?appt_id=12 or ?email=user@example.com)
        string? initialLookup = !string.IsNullOrWhiteSpace(query) ? query : (!string.IsNullOrWhiteSpace(email) ? email : phone);
        if (!string.IsNullOrWhiteSpace(initialLookup))
        {
            searchPhoneInput = initialLookup.Trim();
            LookupCustomer();
            if (activeCustomer != null) return;
        }

        if (!string.IsNullOrWhiteSpace(appt_id) && int.TryParse(appt_id, out int parsedApptId))
        {
            var matched = SalonService.Appointments.FirstOrDefault(a => a.Id == parsedApptId);
            if (matched != null)
            {
                activeCustomer = (matched.CustomerId.HasValue ? SalonService.Customers.FirstOrDefault(c => c.Id == matched.CustomerId.Value) : null) ??
                                 SalonService.FindCustomerByPhoneOrName(matched.ClientEmail) ??
                                 SalonService.FindCustomerByPhoneOrName(matched.ClientPhone) ??
                                 SalonService.EnsureCustomer(matched.ClientName, matched.ClientPhone, matched.ClientEmail);
                if (activeCustomer != null)
                {
                    lookupMessage = $"Showing appointment records for {activeCustomer.FullName}.";
                    return;
                }
            }
        }

        // 2. If customer is currently logged in, auto-load or ensure their profile
        if (SalonService.CurrentUser.IsLoggedIn)
        {
            var userEmail = SalonService.CurrentUser.Email?.Trim() ?? "";
            var userName = SalonService.CurrentUser.Name?.Trim() ?? "";

            if (!string.IsNullOrEmpty(userEmail))
            {
                activeCustomer = SalonService.FindCustomerByPhoneOrName(userEmail);
            }
            if (activeCustomer == null && !string.IsNullOrEmpty(userName))
            {
                activeCustomer = SalonService.FindCustomerByPhoneOrName(userName);
            }

            // Ensure profile exists for the logged in user
            if (activeCustomer == null && !string.IsNullOrEmpty(userName))
            {
                activeCustomer = SalonService.EnsureCustomer(userName, "", userEmail);
            }

            if (activeCustomer != null)
            {
                lookupMessage = $"Welcome back, {activeCustomer.FullName}! Here are your scheduled rituals and loyalty rewards.";
                return;
            }
        }

        // 3. If not logged in, check for the most recent appointment booked in this session
        var latestAppt = SalonService.Appointments.OrderByDescending(a => a.Id).FirstOrDefault();
        if (latestAppt != null && !string.IsNullOrWhiteSpace(latestAppt.ClientName))
        {
            activeCustomer = (latestAppt.CustomerId.HasValue ? SalonService.Customers.FirstOrDefault(c => c.Id == latestAppt.CustomerId.Value) : null) ??
                             SalonService.FindCustomerByPhoneOrName(latestAppt.ClientEmail) ??
                             SalonService.FindCustomerByPhoneOrName(latestAppt.ClientPhone) ??
                             SalonService.FindCustomerByPhoneOrName(latestAppt.ClientName);
            if (activeCustomer != null)
            {
                lookupMessage = $"Viewing latest appointment records for {activeCustomer.FullName}.";
                return;
            }
        }

        // 4. Default preview profile for demonstration
        if (activeCustomer == null && SalonService.Customers.Any())
        {
            activeCustomer = SalonService.Customers.FirstOrDefault(c => c.Tier == "Platinum") ?? 
                             SalonService.Customers.FirstOrDefault(c => c.Tier == "Gold") ?? 
                             SalonService.Customers.First();
            lookupMessage = $"Showing preview profile for {activeCustomer.FullName}. Enter your phone or email above to view your bookings.";
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
            lookupMessage = "Please enter your mobile phone number, email address, or full name.";
            return;
        }

        SalonService.EnsureSeedData();
        var q = searchPhoneInput.Trim();
        var found = SalonService.FindCustomerByPhoneOrName(q);

        // If not found directly in Customers, check if any appointment matches the query
        if (found == null)
        {
            var normQ = SalonDataService.NormalizePhoneNumber(q);
            var apptMatch = SalonService.Appointments.FirstOrDefault(a =>
                (!string.IsNullOrWhiteSpace(a.ClientEmail) && a.ClientEmail.Equals(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(a.ClientPhone) && !string.IsNullOrEmpty(normQ) && normQ.Length >= 4 && SalonDataService.NormalizePhoneNumber(a.ClientPhone).Contains(normQ)) ||
                (!string.IsNullOrWhiteSpace(a.ClientName) && a.ClientName.Equals(q, StringComparison.OrdinalIgnoreCase)));

            if (apptMatch != null)
            {
                found = SalonService.EnsureCustomer(apptMatch.ClientName, apptMatch.ClientPhone, apptMatch.ClientEmail);
            }
        }

        if (found != null)
        {
            activeCustomer = found;
            lookupMessage = $"Found loyalty profile for {activeCustomer.FullName} ({activeCustomer.ClientCode})!";
        }
        else
        {
            lookupMessage = $"No customer or appointments found matching '{searchPhoneInput}'. You can book an appointment anytime!";
        }
    }

    private void ClearSearch()
    {
        searchPhoneInput = "";
        if (SalonService.CurrentUser.IsLoggedIn)
        {
            var userEmail = SalonService.CurrentUser.Email?.Trim() ?? "";
            var userName = SalonService.CurrentUser.Name?.Trim() ?? "";
            activeCustomer = (!string.IsNullOrEmpty(userEmail) ? SalonService.FindCustomerByPhoneOrName(userEmail) : null) ??
                             (!string.IsNullOrEmpty(userName) ? SalonService.FindCustomerByPhoneOrName(userName) : null);
            lookupMessage = activeCustomer != null ? $"Viewing profile for {activeCustomer.FullName}" : "";
        }
        else
        {
            lookupMessage = "";
        }
    }

    private IEnumerable<AppointmentRecord> CustomerAppointments
    {
        get
        {
            if (activeCustomer == null && !SalonService.CurrentUser.IsLoggedIn)
            {
                return Enumerable.Empty<AppointmentRecord>();
            }

            var activePhone = activeCustomer?.Phone ?? "";
            var normalizedActivePhone = SalonDataService.NormalizePhoneNumber(activePhone);
            var activeEmail = activeCustomer?.Email?.Trim() ?? "";
            var activeName = activeCustomer?.FullName?.Trim() ?? "";
            var activeId = activeCustomer?.Id ?? -1;

            var userEmail = SalonService.CurrentUser.IsLoggedIn ? (SalonService.CurrentUser.Email?.Trim() ?? "") : "";
            var userName = SalonService.CurrentUser.IsLoggedIn ? (SalonService.CurrentUser.Name?.Trim() ?? "") : "";

            return SalonService.Appointments
                .Where(a =>
                    (activeId > 0 && a.CustomerId.HasValue && a.CustomerId.Value == activeId) ||
                    (!string.IsNullOrEmpty(activeEmail) && !string.IsNullOrEmpty(a.ClientEmail) && a.ClientEmail.Equals(activeEmail, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(a.ClientEmail) && a.ClientEmail.Equals(userEmail, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(normalizedActivePhone) && !string.IsNullOrEmpty(a.ClientPhone) && SalonDataService.NormalizePhoneNumber(a.ClientPhone) == normalizedActivePhone) ||
                    (!string.IsNullOrEmpty(activeName) && !string.IsNullOrEmpty(a.ClientName) && a.ClientName.Equals(activeName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(a.ClientName) && a.ClientName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(a => a.Date >= DateTime.Today && !a.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(a => a.Date)
                .ThenByDescending(a => a.Id);
        }
    }

    private IEnumerable<InvoiceRecord> CustomerInvoices
    {
        get
        {
            if (activeCustomer == null && !SalonService.CurrentUser.IsLoggedIn)
            {
                return Enumerable.Empty<InvoiceRecord>();
            }

            var activeName = activeCustomer?.FullName?.Trim() ?? "";
            var activeId = activeCustomer?.Id ?? -1;
            var userName = SalonService.CurrentUser.IsLoggedIn ? (SalonService.CurrentUser.Name?.Trim() ?? "") : "";

            return SalonService.Invoices
                .Where(i =>
                    (activeId > 0 && i.CustomerId.HasValue && i.CustomerId.Value == activeId) ||
                    (!string.IsNullOrEmpty(activeName) && !string.IsNullOrEmpty(i.ClientName) && i.ClientName.Equals(activeName, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(i.ClientName) && i.ClientName.Equals(userName, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(i => i.Timestamp);
        }
    }

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

    private void PromptCancelAppt(AppointmentRecord appt)
    {
        apptToCancel = appt;
        portalActionFeedback = null;
    }

    private void AbortCancelAppt()
    {
        apptToCancel = null;
    }

    private async Task ConfirmPortalCancelAppointment()
    {
        if (apptToCancel == null) return;
        isCancellingAppt = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(cancelReason))
            {
                var reasonTag = $"[Cancelled by Client: {cancelReason}]";
                var updatedNotes = string.IsNullOrWhiteSpace(apptToCancel.Notes)
                    ? reasonTag
                    : $"{apptToCancel.Notes} | {reasonTag}";
                SalonService.UpdateAppointmentNotes(apptToCancel.Id, updatedNotes);
            }

            SalonService.UpdateAppointmentStatus(apptToCancel.Id, "Cancelled");
            apptToCancel.Status = "Cancelled";

            if (!string.IsNullOrWhiteSpace(apptToCancel.ClientEmail))
            {
                _ = EmailService.SendBookingCancellationAsync(apptToCancel, apptToCancel.ClientEmail, cancelReason);
            }

            portalActionFeedback = $"Appointment #APT-{apptToCancel.Id:D5} for {apptToCancel.ServiceName} was successfully cancelled.";
            isPortalFeedbackSuccess = true;
            apptToCancel = null;
        }
        catch (Exception ex)
        {
            portalActionFeedback = $"Error cancelling appointment: {ex.Message}";
            isPortalFeedbackSuccess = false;
        }
        finally
        {
            isCancellingAppt = false;
            StateHasChanged();
        }
    }

    private void HandleLogout()
    {
        SalonService.Logout();
        Navigation.NavigateTo("/login");
    }
}
