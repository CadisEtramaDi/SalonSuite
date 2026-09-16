using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class Login : ComponentBase
{
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public FirebaseAuthService FirebaseAuth { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

    private bool isRegisterMode = false;
    private string fullNameInput = "";
    private string phoneInput = "";
    private string emailInput = "";
    private string passwordInput = "";
    private string confirmPasswordInput = "";
    private bool showPassword = false;
    private bool rememberMe = true;
    private string errorMessage = "";
    private bool registerSuccess = false;
    private bool isAuthenticating = false;
    private string? returnUrl = null;

    protected override void OnInitialized()
    {
        var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("returnUrl", out var rUrl) && !string.IsNullOrWhiteSpace(rUrl))
        {
            returnUrl = rUrl.ToString();
        }

        if (uri.AbsolutePath.ToLower().Contains("/register") || (query.TryGetValue("mode", out var mode) && mode == "register"))
        {
            isRegisterMode = true;
        }
    }

    private void SwitchMode(bool register)
    {
        isRegisterMode = register;
        errorMessage = "";
        registerSuccess = false;
        isAuthenticating = false;
    }

    private async Task HandleGoogleSignIn()
    {
        errorMessage = "";
        isAuthenticating = true;
        StateHasChanged();

        try
        {
            var result = await JSRuntime.InvokeAsync<GoogleAuthPayload>("salonFirebaseAuth.signInWithGoogle");
            if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.Email))
            {
                errorMessage = result?.ErrorMessage ?? "Google Sign-In was cancelled or failed.";
                isAuthenticating = false;
                return;
            }

            // Verify Google token with Firebase backend if configured
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

            string redirectUrl = GetRedirectUrlForRole(role);
            Navigation.NavigateTo(redirectUrl);
        }
        catch (Exception ex)
        {
            errorMessage = $"Google sign in error: {ex.Message}";
            isAuthenticating = false;
        }
    }

    private async Task HandleLogin()
    {
        errorMessage = "";
        if (string.IsNullOrWhiteSpace(emailInput))
        {
            errorMessage = "Please enter your email address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordInput))
        {
            errorMessage = "Please enter your password.";
            return;
        }

        isAuthenticating = true;

        if (FirebaseAuth.IsConfigured)
        {
            var result = await FirebaseAuth.SignInWithEmailPasswordAsync(emailInput, passwordInput);
            if (!result.Success)
            {
                errorMessage = result.ErrorMessage ?? "Authentication failed.";
                isAuthenticating = false;
                return;
            }
        }

        isAuthenticating = false;

        string username = "User";
        if (!string.IsNullOrWhiteSpace(emailInput))
        {
            var parts = emailInput.Trim().Split('@');
            if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
            {
                username = char.ToUpper(parts[0][0]) + (parts[0].Length > 1 ? parts[0].Substring(1) : "");
            }
        }

        // 1. Check if email matches a registered Team/Staff member first
        var staffMember = SalonService.Team.FirstOrDefault(t => t.Email.Equals(emailInput, StringComparison.OrdinalIgnoreCase));
        if (staffMember != null)
        {
            username = staffMember.Name;
        }
        else if (emailInput.Equals("francisarnejo67@gmail.com", StringComparison.OrdinalIgnoreCase) ||
                 emailInput.Equals("francisarnejo@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            username = "Francis Arnejo (Owner)";
        }
        else if (emailInput.Equals("admin@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            username = "Salon Administrator";
        }
        else if (emailInput.Equals("cashier@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            username = "Clara (Cashier)";
        }
        else if (emailInput.Contains("employee", StringComparison.OrdinalIgnoreCase))
        {
            username = "Sofia Martinez";
        }

        string role = DetermineUserRole(emailInput);
        if (role != "Customer")
        {
            SalonService.EnsureEmployeeExists(username, emailInput, role);
        }
        SalonService.LoginAs(role, username, emailInput);

        string redirectUrl = GetRedirectUrlForRole(role);
        Navigation.NavigateTo(redirectUrl);
    }

    private async Task HandleCustomerRegister()
    {
        errorMessage = "";

        if (string.IsNullOrWhiteSpace(fullNameInput))
        {
            errorMessage = "Please enter your full name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(emailInput))
        {
            errorMessage = "Please enter your email address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordInput))
        {
            errorMessage = "Please enter a password.";
            return;
        }

        if (passwordInput.Length < 6)
        {
            errorMessage = "Password must be at least 6 characters in Firebase.";
            return;
        }

        if (passwordInput != confirmPasswordInput)
        {
            errorMessage = "Passwords do not match. Please verify your password.";
            return;
        }

        isAuthenticating = true;

        if (FirebaseAuth.IsConfigured)
        {
            var result = await FirebaseAuth.SignUpWithEmailPasswordAsync(emailInput, passwordInput);
            if (!result.Success)
            {
                errorMessage = result.ErrorMessage ?? "Registration failed.";
                isAuthenticating = false;
                return;
            }
        }

        isAuthenticating = false;

        string assignedRole = DetermineUserRole(emailInput);
        if (assignedRole != "Customer")
        {
            SalonService.EnsureEmployeeExists(fullNameInput, emailInput, assignedRole);
        }
        else
        {
            SalonService.EnsureCustomer(fullNameInput, phoneInput, emailInput);
        }

        SalonService.LoginAs(assignedRole, fullNameInput, emailInput);
        registerSuccess = true;

        await Task.Delay(1200);
        string redirectUrl = GetRedirectUrlForRole(assignedRole);
        Navigation.NavigateTo(redirectUrl);
    }

    private string GetRedirectUrlForRole(string role)
    {
        if (role == "Customer")
        {
            return !string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : "/";
        }

        return role switch
        {
            "Salon Owner / Admin" or "Salon Manager" => "/admin",
            "Cashier / Front Desk" or "Front Desk Cashier" => "/cashier",
            "Stylist / Specialist" or "Staff Member" or "Senior Stylist" or "Color Specialist" or "Creative Director" or "Junior Stylist" => "/employee",
            _ => !string.IsNullOrWhiteSpace(returnUrl) ? returnUrl : "/"
        };
    }

    private string DetermineUserRole(string email)
    {
        // 1. Explicit owner & admin check
        if (email.Equals("francisarnejo67@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            email.Equals("francisarnejo@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            return "Salon Owner / Admin";
        }

        // 2. Check if email belongs to a team member in the salon roster
        var member = SalonService.Team.FirstOrDefault(t => t.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        if (member != null)
        {
            if (member.Role.Contains("Cashier", StringComparison.OrdinalIgnoreCase) || member.Role.Contains("Front Desk", StringComparison.OrdinalIgnoreCase))
            {
                return "Cashier / Front Desk";
            }
            if (member.Role.Contains("Manager", StringComparison.OrdinalIgnoreCase) || member.Role.Contains("Owner", StringComparison.OrdinalIgnoreCase))
            {
                return "Salon Owner / Admin";
            }
            return "Stylist / Specialist";
        }

        // 3. Standard heuristic matching
        if (email.Contains("admin", StringComparison.OrdinalIgnoreCase) ||
            email.Contains("owner", StringComparison.OrdinalIgnoreCase))
        {
            return "Salon Owner / Admin";
        }
        if (email.Contains("cashier", StringComparison.OrdinalIgnoreCase) ||
            email.Contains("pos", StringComparison.OrdinalIgnoreCase) ||
            email.Contains("billing", StringComparison.OrdinalIgnoreCase))
        {
            return "Cashier / Front Desk";
        }
        if (email.Contains("stylist", StringComparison.OrdinalIgnoreCase) ||
            email.Contains("specialist", StringComparison.OrdinalIgnoreCase))
        {
            return "Stylist / Specialist";
        }
        if (email.Contains("manager", StringComparison.OrdinalIgnoreCase))
        {
            return "Salon Manager";
        }
        if (email.Contains("staff", StringComparison.OrdinalIgnoreCase) ||
            email.Contains("employee", StringComparison.OrdinalIgnoreCase) ||
            email.EndsWith("@beautyhair.com", StringComparison.OrdinalIgnoreCase) ||
            email.EndsWith("@beautyhair.ph", StringComparison.OrdinalIgnoreCase))
        {
            return "Staff Member";
        }

        return "Customer";
    }

    private async Task HandleForgotPassword()
    {
        if (string.IsNullOrWhiteSpace(emailInput))
        {
            errorMessage = "Please enter your email address above, then click 'Forgot password?'.";
            return;
        }

        if (FirebaseAuth.IsConfigured)
        {
            var result = await FirebaseAuth.SendPasswordResetEmailAsync(emailInput);
            if (result.Success)
            {
                errorMessage = $"Password reset email has been sent to {emailInput}.";
            }
            else
            {
                errorMessage = result.ErrorMessage ?? "Failed to send password reset email.";
            }
        }
        else
        {
            errorMessage = $"Password reset request received for {emailInput}. (Add Firebase API Key to send live email).";
        }
    }
}
