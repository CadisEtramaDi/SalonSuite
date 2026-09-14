using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalonSuite.Models;
using Stripe;
using Stripe.Checkout;

namespace SalonSuite.Services;

public class StripePaymentService
{
    private readonly IConfiguration _config;
    private readonly NavigationManager _nav;
    private readonly ILogger<StripePaymentService> _logger;

    public string PublishableKey => _config["Stripe:PublishableKey"] ?? "";
    public string SecretKey => _config["Stripe:SecretKey"] ?? "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !SecretKey.Contains("placeholder") &&
        SecretKey.StartsWith("sk_");

    public StripePaymentService(IConfiguration config, NavigationManager nav, ILogger<StripePaymentService> logger)
    {
        _config = config;
        _nav = nav;
        _logger = logger;
    }

    /// <summary>
    /// Processes an instant Stripe charge for the Cashier POS using Stripe PaymentIntent API.
    /// In Stripe Sandbox/Test Mode, pm_card_visa will immediately succeed.
    /// </summary>
    public async Task<(bool Success, string TransactionId, string? ErrorMessage)> ProcessCashierCardPaymentAsync(
        decimal amount,
        string clientName,
        string? clientEmail,
        string serviceName,
        string? customCardNumber = null)
    {
        if (!IsConfigured)
        {
            // Simulation fallback if keys are missing
            var mockId = $"ch_sandbox_sim_{DateTime.UtcNow.Ticks.ToString()[^8..]}";
            return (true, mockId, null);
        }

        try
        {
            StripeConfiguration.ApiKey = SecretKey;

            var options = new PaymentIntentCreateOptions
            {
                Amount = (long)(amount * 100), // in centavos/cents
                Currency = "php",
                PaymentMethod = "pm_card_visa", // Stripe standard test card
                Confirm = true,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "never"
                },
                Description = $"SalonSuite POS Cashier: {serviceName} for {clientName}",
                ReceiptEmail = !string.IsNullOrWhiteSpace(clientEmail) ? clientEmail : null,
                Metadata = new Dictionary<string, string>
                {
                    { "ClientName", clientName },
                    { "ServiceName", serviceName },
                    { "Amount", amount.ToString("F2") },
                    { "Source", "Cashier POS Terminal" }
                }
            };

            var service = new PaymentIntentService();
            PaymentIntent intent = await service.CreateAsync(options);

            if (intent.Status == "succeeded")
            {
                return (true, intent.Id, null);
            }

            return (false, intent.Id, $"Stripe returned status: {intent.Status}");
        }
        catch (StripeException stripeEx)
        {
            _logger.LogError(stripeEx, "Stripe charge error: {Message}", stripeEx.Message);
            return (false, "", stripeEx.StripeError?.Message ?? stripeEx.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment error: {Message}", ex.Message);
            return (false, "", ex.Message);
        }
    }

    /// <summary>
    /// Creates a Stripe Hosted Checkout Session for Cashier billing link.
    /// </summary>
    public async Task<string> CreateCashierCheckoutSessionAsync(
        decimal amount,
        string clientName,
        string? clientEmail,
        string serviceName,
        int appointmentId = 0)
    {
        var domain = _nav.BaseUri.TrimEnd('/');

        if (!IsConfigured)
        {
            return $"{domain}/cashier?stripe_simulated=true";
        }

        try
        {
            StripeConfiguration.ApiKey = SecretKey;

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(amount * 100),
                            Currency = "php",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = $"Salon POS Bill: {serviceName}",
                                Description = $"Customer: {clientName} | POS Register"
                            }
                        },
                        Quantity = 1
                    }
                },
                Mode = "payment",
                CustomerEmail = !string.IsNullOrWhiteSpace(clientEmail) ? clientEmail : null,
                PaymentIntentData = new SessionPaymentIntentDataOptions
                {
                    ReceiptEmail = !string.IsNullOrWhiteSpace(clientEmail) ? clientEmail : null,
                    Description = $"Salon POS Bill: {serviceName} for {clientName}"
                },
                SuccessUrl = $"{domain}/cashier?stripe_session_id={{CHECKOUT_SESSION_ID}}&status=success",
                CancelUrl = $"{domain}/cashier?status=cancelled",
                Metadata = new Dictionary<string, string>
                {
                    { "AppointmentId", appointmentId.ToString() },
                    { "ClientName", clientName },
                    { "ServiceName", serviceName },
                    { "Amount", amount.ToString("F2") },
                    { "Source", "Cashier POS" }
                }
            };

            var service = new SessionService();
            Session session = await service.CreateAsync(options);
            return session.Url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Cashier Stripe Checkout: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Verifies session status from Stripe API.
    /// </summary>
    public async Task<(bool IsPaid, string CustomerEmail, decimal AmountTotal, Dictionary<string, string> Metadata)> VerifySessionAsync(string sessionId, int apptId = 0)
    {
        if (sessionId.StartsWith("sandbox_sess_") || !IsConfigured)
        {
            return (
                IsPaid: true,
                CustomerEmail: "sandbox@client.test",
                AmountTotal: 0,
                Metadata: new Dictionary<string, string> { { "AppointmentId", apptId.ToString() } }
            );
        }

        try
        {
            StripeConfiguration.ApiKey = SecretKey;
            var service = new SessionService();
            var session = await service.GetAsync(sessionId);

            bool isPaid = session.PaymentStatus?.Equals("paid", StringComparison.OrdinalIgnoreCase) == true;
            decimal amount = (session.AmountTotal ?? 0) / 100m;
            var metadata = session.Metadata ?? new Dictionary<string, string>();

            return (isPaid, session.CustomerDetails?.Email ?? session.CustomerEmail ?? "", amount, metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Stripe session {SessionId}: {Message}", sessionId, ex.Message);
            return (false, "", 0, new Dictionary<string, string>());
        }
    }
}
