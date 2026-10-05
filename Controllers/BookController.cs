using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class Book : ComponentBase, IDisposable
{
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public FirebaseAuthService FirebaseAuth { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] public EmailReceiptService EmailService { get; set; } = default!;

    private string selectedCategory = "All";
    private string searchTerm = "";
    private string sortBy = "featured";

    private bool showLoginRequiredModal = false;
    private bool isAuthRegisterMode = false;
    private string pendingBookingService = "";
    private string authFullName = "";
    private string authPhone = "";
    private string authEmail = "";
    private string authPassword = "";
    private bool showAuthPassword = false;
    private string authErrorMessage = "";
    private bool authRegisterSuccess = false;
    private bool isAuthenticating = false;

    private bool showBookingModal = false;
    private bool bookingConfirmed = false;
    private bool isCancellingConfirmedAppt = false;
    private bool bookingCancelled = false;
    private string cancelReason = "Change of schedule";
    private bool isProcessingCancellation = false;
    private string? cancelErrorMessage = null;
    private bool wasAutoAssigned = false;
    private AppointmentRecord newAppt = new();

    private readonly List<string> availableTimeSlots = new()
    {
        "09:00 AM", "10:30 AM", "01:00 PM", "02:30 PM", "04:00 PM", "05:30 PM", "07:00 PM"
    };

    private CustomerRecord? CurrentLoggedInCustomer =>
        SalonService.CurrentUser.IsLoggedIn
            ? SalonService.Customers.FirstOrDefault(c =>
                (!string.IsNullOrEmpty(c.Email) && c.Email.Equals(SalonService.CurrentUser.Email, StringComparison.OrdinalIgnoreCase)) ||
                c.FullName.Equals(SalonService.CurrentUser.Name, StringComparison.OrdinalIgnoreCase))
            : null;

    protected override void OnInitialized()
    {
        SalonService.OnChange += HandleDataChanged;
        ParseQueryParameters();
    }

    private void ParseQueryParameters()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("category", out var catValue))
        {
            selectedCategory = catValue.ToString();
        }

        string? selectedServiceParam = null;
        if (query.TryGetValue("select", out var selectValue) && !string.IsNullOrWhiteSpace(selectValue))
        {
            selectedServiceParam = selectValue.ToString();
        }
        else if (query.TryGetValue("package", out var pkgValue) && !string.IsNullOrWhiteSpace(pkgValue))
        {
            selectedServiceParam = pkgValue.ToString();
        }
        else if (query.TryGetValue("service", out var svcValue) && !string.IsNullOrWhiteSpace(svcValue))
        {
            selectedServiceParam = svcValue.ToString();
        }

        if (!string.IsNullOrWhiteSpace(selectedServiceParam))
        {
            OpenBookingModal(selectedServiceParam);
        }
    }

    private void SetCategory(string category)
    {
        selectedCategory = category;
    }

    private void ResetFilters()
    {
        selectedCategory = "All";
        searchTerm = "";
        sortBy = "featured";
    }

    private List<ServicePackageItem> FilteredPackages
    {
        get
        {
            if (selectedCategory != "All" && selectedCategory != "Packages")
            {
                return new List<ServicePackageItem>();
            }

            var query = SalonService.Packages.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(p =>
                    p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    p.Features.Any(f => f.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            return (sortBy switch
            {
                "price-asc" => query.OrderBy(p => p.Price),
                "price-desc" => query.OrderByDescending(p => p.Price),
                "duration" => query.OrderBy(p => p.DurationMinutes),
                _ => query.OrderByDescending(p => p.IsPopular).ThenBy(p => p.Id)
            }).ToList();
        }
    }

    private List<ServiceOfferItem> FilteredServices
    {
        get
        {
            if (selectedCategory == "Packages")
            {
                return new List<ServiceOfferItem>();
            }

            var query = SalonService.Services.Where(s => s.IsActive).AsEnumerable();

            if (selectedCategory != "All")
            {
                query = query.Where(s => s.Category.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                query = query.Where(s =>
                    s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Category.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Number.Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            return (sortBy switch
            {
                "price-asc" => query.OrderBy(s => s.Price),
                "price-desc" => query.OrderByDescending(s => s.Price),
                "duration" => query.OrderBy(s => s.DurationMinutes),
                _ => query.OrderBy(s => s.Id)
            }).ToList();
        }
    }

    private void SwitchAuthMode(bool isRegister)
    {
        isAuthRegisterMode = isRegister;
        authErrorMessage = "";
        authRegisterSuccess = false;
        isAuthenticating = false;
    }

    private async Task HandleModalGoogleSignIn()
    {
        authErrorMessage = "";
        isAuthenticating = true;
        StateHasChanged();

        try
        {
            var result = await JSRuntime.InvokeAsync<GoogleAuthPayload>("salonFirebaseAuth.signInWithGoogle");
            if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.Email))
            {
                authErrorMessage = result?.ErrorMessage ?? "Google Sign-In was cancelled or failed.";
                isAuthenticating = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(result.IdToken) && FirebaseAuth.IsConfigured)
            {
                await FirebaseAuth.SignInWithGoogleIdTokenAsync(result.IdToken);
            }

            string email = result.Email.Trim().ToLowerInvariant();
            string displayName = !string.IsNullOrWhiteSpace(result.DisplayName) ? result.DisplayName : email.Split('@')[0];

            if (email.Equals("francisarnejo67@gmail.com", StringComparison.OrdinalIgnoreCase) || 
                email.Equals("francisarnejo@gmail.com", StringComparison.OrdinalIgnoreCase))
            {
                displayName = "Francis Arnejo (Owner)";
            }

            string role = DetermineUserRole(email);
            if (role != "Customer")
            {
                SalonService.EnsureEmployeeExists(displayName, email, role);
            }
            else
            {
                SalonService.EnsureCustomer(displayName, result.PhoneNumber ?? "", email);
            }

            SalonService.LoginAs(role, displayName, email);
            isAuthenticating = false;
            showLoginRequiredModal = false;

            // Open booking modal
            OpenBookingModal(pendingBookingService);
            StateHasChanged();
        }
        catch (Exception ex)
        {
            authErrorMessage = $"Google sign in error: {ex.Message}";
            isAuthenticating = false;
        }
    }

    private async Task HandleModalLogin()
    {
        authErrorMessage = "";
        if (string.IsNullOrWhiteSpace(authEmail))
        {
            authErrorMessage = "Please enter your email address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(authPassword))
        {
            authErrorMessage = "Please enter your password.";
            return;
        }

        isAuthenticating = true;

        if (FirebaseAuth.IsConfigured)
        {
            var result = await FirebaseAuth.SignInWithEmailPasswordAsync(authEmail, authPassword);
            if (!result.Success)
            {
                authErrorMessage = result.ErrorMessage ?? "Authentication failed.";
                isAuthenticating = false;
                return;
            }
        }

        isAuthenticating = false;

        string username = "Client";
        if (!string.IsNullOrWhiteSpace(authEmail))
        {
            var parts = authEmail.Trim().Split('@');
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                username = char.ToUpper(parts[0][0]) + (parts[0].Length > 1 ? parts[0].Substring(1) : "");
            }
        }

        var staffMember = SalonService.Team.FirstOrDefault(t => t.Email.Equals(authEmail, StringComparison.OrdinalIgnoreCase));
        if (staffMember != null)
        {
            username = staffMember.Name;
        }

        var customerRecord = SalonService.Customers.FirstOrDefault(c => !string.IsNullOrEmpty(c.Email) && c.Email.Equals(authEmail, StringComparison.OrdinalIgnoreCase));
        if (customerRecord != null)
        {
            username = customerRecord.FullName;
        }

        string role = DetermineUserRole(authEmail);
        if (role != "Customer")
        {
            SalonService.EnsureEmployeeExists(username, authEmail, role);
        }
        else
        {
            SalonService.EnsureCustomer(username, "", authEmail);
        }

        SalonService.LoginAs(role, username, authEmail);
        showLoginRequiredModal = false;

        // Open booking modal
        OpenBookingModal(pendingBookingService);
        StateHasChanged();
    }

    private async Task HandleModalRegister()
    {
        authErrorMessage = "";

        if (string.IsNullOrWhiteSpace(authFullName))
        {
            authErrorMessage = "Please enter your full name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(authPhone))
        {
            authErrorMessage = "Please enter your mobile phone number.";
            return;
        }

        if (string.IsNullOrWhiteSpace(authEmail))
        {
            authErrorMessage = "Please enter your email address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(authPassword))
        {
            authErrorMessage = "Please enter a password.";
            return;
        }

        if (authPassword.Length < 6)
        {
            authErrorMessage = "Password must be at least 6 characters in Firebase.";
            return;
        }

        isAuthenticating = true;

        if (FirebaseAuth.IsConfigured)
        {
            var result = await FirebaseAuth.SignUpWithEmailPasswordAsync(authEmail, authPassword);
            if (!result.Success)
            {
                authErrorMessage = result.ErrorMessage ?? "Registration failed.";
                isAuthenticating = false;
                return;
            }
        }

        isAuthenticating = false;

        string assignedRole = DetermineUserRole(authEmail);
        if (assignedRole != "Customer")
        {
            SalonService.EnsureEmployeeExists(authFullName, authEmail, assignedRole);
        }
        else
        {
            SalonService.EnsureCustomer(authFullName, authPhone, authEmail);
        }

        SalonService.LoginAs(assignedRole, authFullName, authEmail);
        authRegisterSuccess = true;

        await Task.Delay(800);
        showLoginRequiredModal = false;
        authRegisterSuccess = false;

        // Open booking modal
        OpenBookingModal(pendingBookingService);
        StateHasChanged();
    }

    private string DetermineUserRole(string email)
    {
        if (email.Equals("francisarnejo67@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            email.Equals("francisarnejo@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            email.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
            email.Contains("owner", StringComparison.OrdinalIgnoreCase))
        {
            return "Salon Owner / Admin";
        }

        var member = SalonService.Team.FirstOrDefault(t => t.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (member != null)
        {
            if (member.Role.Contains("Cashier", StringComparison.OrdinalIgnoreCase) || member.Role.Contains("Front Desk", StringComparison.OrdinalIgnoreCase))
                return "Cashier / Front Desk";
            if (member.Role.Contains("Manager", StringComparison.OrdinalIgnoreCase) || member.Role.Contains("Owner", StringComparison.OrdinalIgnoreCase))
                return "Salon Owner / Admin";
            return "Stylist / Specialist";
        }

        if (email.Contains("cashier", StringComparison.OrdinalIgnoreCase)) return "Cashier / Front Desk";
        if (email.Contains("stylist", StringComparison.OrdinalIgnoreCase)) return "Stylist / Specialist";
        if (email.Contains("staff", StringComparison.OrdinalIgnoreCase) || email.Contains("employee", StringComparison.OrdinalIgnoreCase)) return "Staff Member";

        return "Customer";
    }

    private void OpenBookingModal(string initialServiceName)
    {
        pendingBookingService = initialServiceName;

        // Ensure the customer is logged in before booking
        if (!SalonService.CurrentUser.IsLoggedIn)
        {
            showBookingModal = false;
            authErrorMessage = "";
            authRegisterSuccess = false;
            showLoginRequiredModal = true;
            return;
        }

        showLoginRequiredModal = false;
        bookingConfirmed = false;
        bookingCancelled = false;
        isCancellingConfirmedAppt = false;
        cancelErrorMessage = null;
        var resolvedService = ResolveServiceSelection(initialServiceName);
        
        var profile = CurrentLoggedInCustomer;
        var customerPhone = profile?.Phone;
        if (string.IsNullOrWhiteSpace(customerPhone))
        {
            var foundCust = SalonService.FindCustomerByPhoneOrName(SalonService.CurrentUser.Email) ??
                            SalonService.FindCustomerByPhoneOrName(SalonService.CurrentUser.Name);
            customerPhone = foundCust?.Phone;
        }

        newAppt = new AppointmentRecord
        {
            ServiceName = resolvedService,
            StylistName = "",
            Date = DateTime.Today.AddDays(1),
            TimeSlot = "10:30 AM",
            Price = GetPriceForSelectedService(resolvedService),
            Status = "Confirmed",
            ClientName = SalonService.CurrentUser.Name,
            ClientEmail = SalonService.CurrentUser.Email,
            ClientPhone = customerPhone ?? ""
        };
        wasAutoAssigned = false;
        showBookingModal = true;
    }

    private string ResolveServiceSelection(string? inputName)
    {
        if (string.IsNullOrWhiteSpace(inputName))
        {
            return "Signature Package";
        }

        var matchedPkg = SalonService.Packages.FirstOrDefault(p =>
            p.Name.Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            $"{p.Name} Package".Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            inputName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedPkg != null)
        {
            return $"{matchedPkg.Name} Package";
        }

        var matchedSvc = SalonService.Services.FirstOrDefault(s =>
            s.Name.Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            $"{s.Number} {s.Name}".Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            inputName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedSvc != null)
        {
            return $"{matchedSvc.Number} {matchedSvc.Name}";
        }

        return "Signature Package";
    }

    private decimal GetPriceForSelectedService(string serviceName)
    {
        var matchedPkg = SalonService.Packages.FirstOrDefault(p =>
            serviceName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedPkg != null) return matchedPkg.Price;

        var matchedSvc = SalonService.Services.FirstOrDefault(s =>
            serviceName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedSvc != null) return matchedSvc.Price;

        return 1990;
    }

    private void OnServiceSelectionChanged()
    {
        newAppt.Price = GetPriceForSelectedService(newAppt.ServiceName);
    }

    private void CloseBookingModalBackdrop()
    {
        CloseBookingModal();
    }

    private void PromptCancelBooking()
    {
        isCancellingConfirmedAppt = true;
        cancelErrorMessage = null;
    }

    private void AbortCancelBooking()
    {
        isCancellingConfirmedAppt = false;
        cancelErrorMessage = null;
    }

    private async Task ConfirmCancelAppointment()
    {
        if (newAppt == null || newAppt.Id == 0) return;

        isProcessingCancellation = true;
        cancelErrorMessage = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(cancelReason))
            {
                var reasonTag = $"[Cancelled by Client: {cancelReason}]";
                var updatedNotes = string.IsNullOrWhiteSpace(newAppt.Notes)
                    ? reasonTag
                    : $"{newAppt.Notes} | {reasonTag}";
                SalonService.UpdateAppointmentNotes(newAppt.Id, updatedNotes);
            }

            SalonService.UpdateAppointmentStatus(newAppt.Id, "Cancelled");
            newAppt.Status = "Cancelled";

            if (!string.IsNullOrWhiteSpace(newAppt.ClientEmail))
            {
                _ = EmailService.SendBookingCancellationAsync(newAppt, newAppt.ClientEmail, cancelReason);
            }

            bookingCancelled = true;
            isCancellingConfirmedAppt = false;
        }
        catch (Exception ex)
        {
            cancelErrorMessage = $"Failed to cancel appointment: {ex.Message}";
        }
        finally
        {
            isProcessingCancellation = false;
            StateHasChanged();
        }
    }

    private void CloseBookingModal()
    {
        showBookingModal = false;
        bookingConfirmed = false;
        bookingCancelled = false;
        isCancellingConfirmedAppt = false;
        cancelErrorMessage = null;
    }

    private void StartNewBookingFromCancelled()
    {
        bookingConfirmed = false;
        bookingCancelled = false;
        isCancellingConfirmedAppt = false;
        cancelErrorMessage = null;
        OpenBookingModal("");
    }

    private void ConfirmBooking()
    {
        if (!SalonService.CurrentUser.IsLoggedIn)
        {
            showBookingModal = false;
            showLoginRequiredModal = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(newAppt.ClientName) || string.IsNullOrWhiteSpace(newAppt.ClientPhone))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(newAppt.StylistName) ||
            newAppt.StylistName.Contains("Any", StringComparison.OrdinalIgnoreCase) ||
            newAppt.StylistName.Contains("No Preference", StringComparison.OrdinalIgnoreCase))
        {
            newAppt.StylistName = SalonService.FindAvailableStylist(newAppt.Date, newAppt.TimeSlot);
            wasAutoAssigned = true;
        }
        else
        {
            wasAutoAssigned = false;
        }

        newAppt.IsPaid = false;
        SalonService.AddAppointment(newAppt);
        bookingConfirmed = true;

        if (!string.IsNullOrWhiteSpace(newAppt.ClientEmail))
        {
            _ = EmailService.SendBookingConfirmationAsync(newAppt, newAppt.ClientEmail);
        }
    }

    private void HandleDataChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void HandleLogout()
    {
        SalonService.Logout();
        StateHasChanged();
    }

    public void Dispose()
    {
        SalonService.OnChange -= HandleDataChanged;
    }
}
