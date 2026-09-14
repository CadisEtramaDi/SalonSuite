using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SalonSuite.Services;

public class XenditPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly NavigationManager _nav;
    private readonly ILogger<XenditPaymentService> _logger;

    public string SecretKey => _config["Xendit:SecretKey"] ?? "";
    public string PublicKey => _config["Xendit:PublicKey"] ?? "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) &&
        !SecretKey.Contains("placeholder") &&
        SecretKey.StartsWith("xnd_");

    public XenditPaymentService(HttpClient httpClient, IConfiguration config, NavigationManager nav, ILogger<XenditPaymentService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _nav = nav;
        _logger = logger;
    }

    /// <summary>
    /// Creates a Xendit Checkout Invoice dedicated for GCash.
    /// Returns the Xendit checkout URL where the client authorizes the GCash payment.
    /// </summary>
    public async Task<XenditInvoiceResult> CreateGcashInvoiceAsync(
        decimal amount,
        string clientName,
        string? clientEmail,
        string serviceName,
        int appointmentId = 0,
        string returnPath = "cashier")
    {
        var domain = _nav.BaseUri.TrimEnd('/');

        if (!IsConfigured)
        {
            // Simulation fallback for sandbox testing when API keys are not yet configured
            var mockInvoiceId = $"xnd_sim_{DateTime.UtcNow.Ticks.ToString()[^8..]}";
            var mockExternalId = $"INV-GCASH-{appointmentId}-{DateTime.UtcNow.Ticks.ToString()[^6..]}";
            return new XenditInvoiceResult
            {
                Success = true,
                InvoiceId = mockInvoiceId,
                ExternalId = mockExternalId,
                InvoiceUrl = $"{domain}/{returnPath}?xendit_simulated=true&xendit_id={mockInvoiceId}&appt_id={appointmentId}&amount={amount:F2}&status=success",
                Status = "PENDING",
                IsSimulated = true
            };
        }

        try
        {
            var externalId = $"INV-GCASH-APPT-{appointmentId}-{DateTime.UtcNow.Ticks.ToString()[^6..]}";

            var payload = new
            {
                external_id = externalId,
                amount = (long)Math.Round(amount, 0),
                currency = "PHP",
                description = $"SalonSuite GCash: {serviceName} for {clientName}",
                payer_email = !string.IsNullOrWhiteSpace(clientEmail) ? clientEmail : "client@salonsuite.ph",
                customer = new
                {
                    given_names = clientName,
                    email = !string.IsNullOrWhiteSpace(clientEmail) ? clientEmail : "client@salonsuite.ph"
                },
                payment_methods = new[] { "GCASH" },
                success_redirect_url = $"{domain}/{returnPath}?xendit_id={externalId}&status=success&appt_id={appointmentId}",
                failure_redirect_url = $"{domain}/{returnPath}?status=failed"
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{SecretKey}:"));
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.xendit.co/v2/invoices")
            {
                Content = jsonContent
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Xendit GCash Invoice creation error ({Status}): {Body}", response.StatusCode, responseBody);
                return new XenditInvoiceResult
                {
                    Success = false,
                    ErrorMessage = $"Xendit Error ({response.StatusCode}): {responseBody}"
                };
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            return new XenditInvoiceResult
            {
                Success = true,
                InvoiceId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "",
                ExternalId = root.TryGetProperty("external_id", out var extProp) ? extProp.GetString() ?? "" : externalId,
                InvoiceUrl = root.TryGetProperty("invoice_url", out var urlProp) ? urlProp.GetString() ?? "" : "",
                Status = root.TryGetProperty("status", out var stProp) ? stProp.GetString() ?? "PENDING" : "PENDING",
                IsSimulated = false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Xendit GCash Invoice: {Message}", ex.Message);
            return new XenditInvoiceResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Verifies the status of a Xendit Invoice by ID or External ID.
    /// </summary>
    public async Task<(bool IsPaid, decimal Amount, string CustomerEmail, string Status)> VerifyInvoiceAsync(string invoiceIdOrExternalId)
    {
        if (string.IsNullOrWhiteSpace(invoiceIdOrExternalId))
        {
            return (false, 0, "", "EMPTY_ID");
        }

        if (invoiceIdOrExternalId.StartsWith("xnd_sim_") || !IsConfigured)
        {
            return (true, 0, "sandbox_gcash@salonsuite.ph", "PAID");
        }

        try
        {
            var authHeader = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{SecretKey}:"));
            
            // Query by invoice ID or external ID
            string url = invoiceIdOrExternalId.StartsWith("INV-") 
                ? $"https://api.xendit.co/v2/invoices?external_id={Uri.EscapeDataString(invoiceIdOrExternalId)}"
                : $"https://api.xendit.co/v2/invoices/{invoiceIdOrExternalId}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Xendit verification failed with status: {Status}", response.StatusCode);
                return (false, 0, "", $"HTTP_{response.StatusCode}");
            }

            var body = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // If queried by external_id, result is an array
            JsonElement invoiceEl = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 
                ? root[0] 
                : root;

            var status = invoiceEl.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "" : "";
            var amount = invoiceEl.TryGetProperty("amount", out var aProp) ? aProp.GetDecimal() : 0m;
            var payerEmail = invoiceEl.TryGetProperty("payer_email", out var eProp) ? eProp.GetString() ?? "" : "";

            bool isPaid = status.Equals("PAID", StringComparison.OrdinalIgnoreCase) || 
                          status.Equals("SETTLED", StringComparison.OrdinalIgnoreCase);

            return (isPaid, amount, payerEmail, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying Xendit invoice {Id}: {Message}", invoiceIdOrExternalId, ex.Message);
            return (false, 0, "", "ERROR");
        }
    }
}

public class XenditInvoiceResult
{
    public bool Success { get; set; }
    public string InvoiceId { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string InvoiceUrl { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsSimulated { get; set; }
    public string? ErrorMessage { get; set; }
    public string QrCodeUrl => !string.IsNullOrWhiteSpace(InvoiceUrl) 
        ? $"https://api.qrserver.com/v1/create-qr-code/?size=250x250&data={Uri.EscapeDataString(InvoiceUrl)}" 
        : "";
}
