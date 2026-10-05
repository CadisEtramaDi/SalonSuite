using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SalonSuite.Models;

namespace SalonSuite.Services;

public class EmailReceiptService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailReceiptService> _logger;

    public string Host => _config["Smtp:Host"] ?? "smtp.gmail.com";
    public int Port => int.TryParse(_config["Smtp:Port"], out var p) ? p : 587;
    public bool EnableSsl => bool.TryParse(_config["Smtp:EnableSsl"], out var s) ? s : true;
    public string SenderEmail => _config["Smtp:SenderEmail"] ?? "beautyhairstudio.pos@gmail.com";
    public string SenderName => _config["Smtp:SenderName"] ?? "Beauty Hair Studio";
    public string Username => _config["Smtp:Username"] ?? "";
    public string Password => _config["Smtp:Password"] ?? "";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) &&
        !string.IsNullOrWhiteSpace(Username) &&
        !string.IsNullOrWhiteSpace(Password) &&
        !Username.Contains("placeholder") &&
        !Password.Contains("placeholder");

    public EmailReceiptService(IConfiguration config, ILogger<EmailReceiptService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Sends an official HTML payment receipt to any specified recipient email.
    /// </summary>
    public async Task<(bool Sent, string? Message)> SendInvoiceReceiptAsync(InvoiceRecord invoice, string? recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains("@"))
        {
            _logger.LogWarning("Cannot send receipt for invoice {InvoiceNumber}: No valid recipient email provided.", invoice.InvoiceNumber);
            return (false, "No valid email address provided.");
        }

        recipientEmail = recipientEmail.Trim();

        if (!IsConfigured)
        {
            _logger.LogInformation(
                "SMTP not configured with real credentials. Receipt for invoice {InvoiceNumber} was simulated for {RecipientEmail}.",
                invoice.InvoiceNumber, recipientEmail);
            return (false, $"SMTP settings not configured. Please add your SMTP/Gmail credentials to appsettings.json to deliver real emails to {recipientEmail}.");
        }

        try
        {
            var htmlBody = GenerateReceiptHtml(invoice, recipientEmail);

            using var message = new MailMessage
            {
                From = new MailAddress(SenderEmail, SenderName),
                Subject = $"Receipt for Your Salon Appointment #{invoice.InvoiceNumber} - Beauty Hair Studio",
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8
            };

            message.To.Add(new MailAddress(recipientEmail, invoice.ClientName));

            using var client = new SmtpClient(Host, Port)
            {
                EnableSsl = EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(Username.Trim(), Password.Replace(" ", "").Trim()),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("Receipt for invoice {InvoiceNumber} successfully sent to {RecipientEmail}", invoice.InvoiceNumber, recipientEmail);
            return (true, $"Receipt sent to {recipientEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send receipt to {RecipientEmail}: {Message}", recipientEmail, ex.Message);
            return (false, $"Failed to send email: {ex.Message}");
        }
    }

    private string GenerateReceiptHtml(InvoiceRecord invoice, string recipientEmail)
    {
        var sb = new StringBuilder();
        sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f7f7f7; margin: 0; padding: 24px; color: #1f2937; }}
        .receipt-card {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 16px rgba(0,0,0,0.06); border: 1px solid #e5e7eb; }}
        .receipt-header {{ background: #111827; color: #ffffff; padding: 32px 28px; text-align: center; }}
        .receipt-brand {{ font-size: 22px; font-weight: 800; letter-spacing: 0.05em; margin: 0; color: #f59e0b; text-transform: uppercase; }}
        .receipt-title {{ font-size: 14px; margin: 6px 0 0 0; color: #d1d5db; letter-spacing: 0.03em; }}
        .receipt-body {{ padding: 28px; }}
        .status-pill {{ display: inline-block; background: #ecfdf5; color: #065f46; font-size: 11.5px; font-weight: 700; padding: 4px 12px; border-radius: 9999px; border: 1px solid #a7f3d0; margin-bottom: 20px; }}
        .meta-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 24px; border-bottom: 1px dashed #e5e7eb; padding-bottom: 20px; }}
        .meta-label {{ font-size: 11px; text-transform: uppercase; color: #6b7280; font-weight: 700; display: block; }}
        .meta-value {{ font-size: 14px; font-weight: 700; color: #111827; margin-top: 2px; }}
        .table-items {{ width: 100%; border-collapse: collapse; margin-bottom: 20px; }}
        .table-items th {{ font-size: 11px; text-transform: uppercase; color: #6b7280; text-align: left; padding: 8px 0; border-bottom: 1px solid #e5e7eb; }}
        .table-items td {{ padding: 12px 0; font-size: 14px; border-bottom: 1px solid #f3f4f6; }}
        .total-row {{ font-size: 18px; font-weight: 800; color: #047857; text-align: right; }}
        .loyalty-banner {{ background: #fef3c7; border: 1px solid #fde68a; color: #92400e; padding: 12px 16px; border-radius: 8px; font-size: 13px; font-weight: 700; margin-bottom: 24px; text-align: center; }}
        .receipt-footer {{ background: #f9fafb; padding: 20px 28px; text-align: center; font-size: 12px; color: #6b7280; border-top: 1px solid #e5e7eb; }}
    </style>
</head>
<body>
    <div class='receipt-card'>
        <div class='receipt-header'>
            <h1 class='receipt-brand'>Beauty Hair Studio</h1>
            <p class='receipt-title'>Official Payment Receipt & Confirmation</p>
        </div>
        <div class='receipt-body'>
            <div style='text-align: center;'>
                <span class='status-pill'>✓ FULLY PAID & SETTLED</span>
            </div>

            <div class='meta-grid'>
                <div>
                    <span class='meta-label'>Invoice Reference</span>
                    <div class='meta-value' style='font-family: monospace;'>{invoice.InvoiceNumber}</div>
                </div>
                <div>
                    <span class='meta-label'>Date & Time</span>
                    <div class='meta-value'>{invoice.Timestamp:MMM dd, yyyy • hh:mm tt}</div>
                </div>
                <div>
                    <span class='meta-label'>Customer Name</span>
                    <div class='meta-value'>{invoice.ClientName}</div>
                </div>
                <div>
                    <span class='meta-label'>Dedicated Specialist</span>
                    <div class='meta-value'>{invoice.StylistName}</div>
                </div>
            </div>

            <table class='table-items'>
                <thead>
                    <tr>
                        <th>Description</th>
                        <th style='text-align: right;'>Amount</th>
                    </tr>
                </thead>
                <tbody>
                    <tr>
                        <td>
                            <strong>{invoice.ServiceName}</strong>
                        </td>
                        <td style='text-align: right; font-weight: 600;'>₱{invoice.Subtotal:N2}</td>
                    </tr>");

        if (invoice.RetailAddonsTotal > 0)
        {
            sb.Append($@"
                    <tr>
                        <td>Retail Products & Add-ons</td>
                        <td style='text-align: right;'>₱{invoice.RetailAddonsTotal:N2}</td>
                    </tr>");
        }

        if (invoice.Discount > 0 || invoice.PromoDiscount > 0 || invoice.LoyaltyDiscount > 0)
        {
            decimal totalDiscounts = invoice.Discount + invoice.PromoDiscount + invoice.LoyaltyDiscount;
            sb.Append($@"
                    <tr style='color: #dc2626;'>
                        <td>Discounts & Reductions</td>
                        <td style='text-align: right;'>-₱{totalDiscounts:N2}</td>
                    </tr>");
        }

        sb.Append($@"
                    <tr>
                        <td style='padding-top: 16px; font-weight: 800; font-size: 15px;'>TOTAL PAID</td>
                        <td style='padding-top: 16px;' class='total-row'>₱{invoice.Total:N2}</td>
                    </tr>
                    <tr>
                        <td style='color: #6b7280; font-size: 12px;'>Payment Method</td>
                        <td style='text-align: right; font-weight: 700; color: #4b5563; font-size: 12.5px;'>{invoice.PaymentMethod}</td>
                    </tr>
                </tbody>
            </table>

            <div class='loyalty-banner'>
                ★ VIP Rewards: +{invoice.PointsEarned} Loyalty Points added to your profile!
            </div>
        </div>

        <div class='receipt-footer'>
            <p style='margin: 0 0 6px 0;'><strong>Beauty Hair Studio</strong> • Luxury Haircare & Spa Rituals</p>
            <p style='margin: 0;'>Thank you for visiting us! Show this receipt at reception for your next booking.</p>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Sends an official HTML appointment confirmation email to the client when a reservation is placed.
    /// </summary>
    public async Task<(bool Sent, string? Message)> SendBookingConfirmationAsync(AppointmentRecord appointment, string? recipientEmail)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains("@"))
        {
            _logger.LogWarning("Cannot send booking confirmation for appointment {ApptId}: No valid recipient email provided.", appointment.Id);
            return (false, "No valid email address provided.");
        }

        recipientEmail = recipientEmail.Trim();

        if (!IsConfigured)
        {
            _logger.LogInformation(
                "SMTP not configured with real credentials. Booking confirmation for appointment #{ApptId} was simulated for {RecipientEmail}.",
                appointment.Id, recipientEmail);
            return (false, $"SMTP settings not configured. Please add your SMTP/Gmail credentials to appsettings.json to deliver real emails to {recipientEmail}.");
        }

        try
        {
            var htmlBody = GenerateBookingConfirmationHtml(appointment, recipientEmail);

            using var message = new MailMessage
            {
                From = new MailAddress(SenderEmail, SenderName),
                Subject = $"Appointment Confirmed: {appointment.ServiceName} ({appointment.Date:MMM dd, yyyy} at {appointment.TimeSlot}) - Beauty Hair Studio",
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8
            };

            message.To.Add(new MailAddress(recipientEmail, appointment.ClientName));

            using var client = new SmtpClient(Host, Port)
            {
                EnableSsl = EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(Username.Trim(), Password.Replace(" ", "").Trim()),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("Booking confirmation for appointment #{ApptId} successfully sent to {RecipientEmail}", appointment.Id, recipientEmail);
            return (true, $"Booking confirmation sent to {recipientEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking confirmation to {RecipientEmail}: {Message}", recipientEmail, ex.Message);
            return (false, $"Failed to send email: {ex.Message}");
        }
    }

    private string GenerateBookingConfirmationHtml(AppointmentRecord appointment, string recipientEmail)
    {
        var sb = new StringBuilder();
        sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f7f7f7; margin: 0; padding: 24px; color: #1f2937; }}
        .receipt-card {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 14px; overflow: hidden; box-shadow: 0 6px 20px rgba(0,0,0,0.07); border: 1px solid #e5e7eb; }}
        .receipt-header {{ background: #111827; color: #ffffff; padding: 36px 28px; text-align: center; }}
        .receipt-brand {{ font-size: 24px; font-weight: 800; letter-spacing: 0.05em; margin: 0; color: #f59e0b; text-transform: uppercase; }}
        .receipt-title {{ font-size: 14px; margin: 6px 0 0 0; color: #d1d5db; letter-spacing: 0.03em; }}
        .receipt-body {{ padding: 30px 28px; }}
        .status-pill {{ display: inline-block; background: #ecfdf5; color: #065f46; font-size: 12px; font-weight: 700; padding: 5px 14px; border-radius: 9999px; border: 1px solid #a7f3d0; margin-bottom: 22px; }}
        .greeting {{ font-size: 16px; font-weight: 600; color: #111827; margin-bottom: 12px; }}
        .intro-text {{ font-size: 14px; color: #4b5563; line-height: 1.5; margin-bottom: 24px; }}
        .meta-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px; border: 1px solid #f3f4f6; background: #fafafa; border-radius: 10px; padding: 18px; }}
        .meta-label {{ font-size: 11px; text-transform: uppercase; color: #6b7280; font-weight: 700; display: block; }}
        .meta-value {{ font-size: 14px; font-weight: 700; color: #111827; margin-top: 2px; }}
        .price-highlight {{ background: #fffbeb; border: 1px solid #fde68a; border-radius: 10px; padding: 16px; margin-bottom: 24px; display: flex; justify-content: space-between; align-items: center; }}
        .price-label {{ font-size: 13px; font-weight: 700; color: #92400e; }}
        .price-amount {{ font-size: 20px; font-weight: 800; color: #b45309; }}
        .pay-notice {{ font-size: 12px; color: #6b7280; margin-top: 4px; }}
        .location-card {{ background: #f9fafb; border-radius: 10px; padding: 16px; margin-bottom: 24px; font-size: 13px; color: #4b5563; line-height: 1.6; border: 1px solid #e5e7eb; }}
        .location-title {{ font-weight: 700; color: #111827; margin-bottom: 4px; display: block; }}
        .receipt-footer {{ background: #111827; color: #9ca3af; padding: 22px 28px; text-align: center; font-size: 12px; }}
        .footer-brand {{ color: #ffffff; font-weight: 700; margin-bottom: 4px; }}
    </style>
</head>
<body>
    <div class='receipt-card'>
        <div class='receipt-header'>
            <h1 class='receipt-brand'>Beauty Hair Studio</h1>
            <p class='receipt-title'>Appointment Confirmation & Reservation Details</p>
        </div>
        <div class='receipt-body'>
            <div style='text-align: center;'>
                <span class='status-pill'>✓ RESERVATION CONFIRMED & SECURED</span>
            </div>

            <div class='greeting'>Hello {appointment.ClientName},</div>
            <p class='intro-text'>
                Your appointment has been successfully booked at <strong>Beauty Hair Studio</strong>! We are excited to pamper you with our luxury salon experience.
            </p>

            <div class='meta-grid'>
                <div>
                    <span class='meta-label'>Selected Service</span>
                    <div class='meta-value' style='color: #b45309;'>{appointment.ServiceName}</div>
                </div>
                <div>
                    <span class='meta-label'>Dedicated Specialist</span>
                    <div class='meta-value'>{appointment.StylistName}</div>
                </div>
                <div>
                    <span class='meta-label'>Appointment Date</span>
                    <div class='meta-value'>{appointment.Date:dddd, MMMM dd, yyyy}</div>
                </div>
                <div>
                    <span class='meta-label'>Scheduled Time Slot</span>
                    <div class='meta-value'>{appointment.TimeSlot}</div>
                </div>
                <div>
                    <span class='meta-label'>Client Contact</span>
                    <div class='meta-value'>{appointment.ClientPhone}</div>
                </div>
                <div>
                    <span class='meta-label'>Reference ID</span>
                    <div class='meta-value' style='font-family: monospace;'>APT-{appointment.Id}</div>
                </div>
            </div>");

        if (!string.IsNullOrWhiteSpace(appointment.Notes))
        {
            sb.Append($@"
            <div style='background: #fdf4ff; border: 1px solid #f5d0fe; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px; font-size: 13px; color: #86198f;'>
                <strong>Your Notes / Requests:</strong> {appointment.Notes}
            </div>");
        }

        sb.Append($@"
            <div class='price-highlight'>
                <div>
                    <div class='price-label'>ESTIMATED SERVICE TOTAL</div>
                    <div class='pay-notice'>Pay in-salon upon arrival (Cash, GCash, or Card at Cashier)</div>
                </div>
                <div class='price-amount'>₱{appointment.Price:N2}</div>
            </div>

            <div class='location-card'>
                <span class='location-title'>📍 Salon Studio Location & Contact</span>
                <div>123 Bonifacio Ave, Taguig City, Metro Manila</div>
                <div><strong>Phone:</strong> +63 917 123 4567 &bull; <strong>Hours:</strong> Mon – Sat: 8:00 AM – 8:00 PM</div>
                <div style='margin-top: 8px; font-size: 12px; color: #6b7280;'>Need to reschedule? Please contact our reception desk at least 24 hours prior to your scheduled slot.</div>
            </div>
        </div>

        <div class='receipt-footer'>
            <div class='footer-brand'>Beauty Hair Studio &bull; Luxury Haircare & Spa Rituals</div>
            <div>Thank you for choosing us! We look forward to seeing you.</div>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Sends an official HTML cancellation confirmation email to the client when a reservation is cancelled.
    /// </summary>
    public async Task<(bool Sent, string? Message)> SendBookingCancellationAsync(AppointmentRecord appointment, string? recipientEmail, string reason = "")
    {
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains("@"))
        {
            _logger.LogWarning("Cannot send booking cancellation for appointment {ApptId}: No valid recipient email provided.", appointment.Id);
            return (false, "No valid email address provided.");
        }

        recipientEmail = recipientEmail.Trim();

        if (!IsConfigured)
        {
            _logger.LogInformation(
                "SMTP not configured with real credentials. Booking cancellation for appointment #{ApptId} was simulated for {RecipientEmail}.",
                appointment.Id, recipientEmail);
            return (false, $"SMTP settings not configured. Please add your SMTP/Gmail credentials to appsettings.json to deliver real emails to {recipientEmail}.");
        }

        try
        {
            var htmlBody = GenerateBookingCancellationHtml(appointment, recipientEmail, reason);

            using var message = new MailMessage
            {
                From = new MailAddress(SenderEmail, SenderName),
                Subject = $"Appointment Cancelled: {appointment.ServiceName} ({appointment.Date:MMM dd, yyyy}) - Beauty Hair Studio",
                Body = htmlBody,
                IsBodyHtml = true,
                BodyEncoding = Encoding.UTF8
            };

            message.To.Add(new MailAddress(recipientEmail, appointment.ClientName));

            using var client = new SmtpClient(Host, Port)
            {
                EnableSsl = EnableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(Username.Trim(), Password.Replace(" ", "").Trim()),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            await client.SendMailAsync(message);
            _logger.LogInformation("Booking cancellation for appointment #{ApptId} successfully sent to {RecipientEmail}", appointment.Id, recipientEmail);
            return (true, $"Booking cancellation sent to {recipientEmail}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send booking cancellation to {RecipientEmail}: {Message}", recipientEmail, ex.Message);
            return (false, $"Failed to send email: {ex.Message}");
        }
    }

    private string GenerateBookingCancellationHtml(AppointmentRecord appointment, string recipientEmail, string reason)
    {
        var sb = new StringBuilder();
        sb.Append($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f7f7f7; margin: 0; padding: 24px; color: #1f2937; }}
        .receipt-card {{ max-width: 580px; margin: 0 auto; background: #ffffff; border-radius: 14px; overflow: hidden; box-shadow: 0 6px 20px rgba(0,0,0,0.07); border: 1px solid #e5e7eb; }}
        .receipt-header {{ background: #1f2937; color: #ffffff; padding: 36px 28px; text-align: center; }}
        .receipt-brand {{ font-size: 24px; font-weight: 800; letter-spacing: 0.05em; margin: 0; color: #f59e0b; text-transform: uppercase; }}
        .receipt-title {{ font-size: 14px; margin: 6px 0 0 0; color: #f87171; letter-spacing: 0.03em; font-weight: 700; }}
        .receipt-body {{ padding: 30px 28px; }}
        .status-pill-cancelled {{ display: inline-block; background: #fef2f2; color: #b91c1c; font-size: 12px; font-weight: 700; padding: 5px 14px; border-radius: 9999px; border: 1px solid #fecaca; margin-bottom: 22px; }}
        .greeting {{ font-size: 16px; font-weight: 600; color: #111827; margin-bottom: 12px; }}
        .intro-text {{ font-size: 14px; color: #4b5563; line-height: 1.5; margin-bottom: 24px; }}
        .meta-grid {{ display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px; border: 1px solid #f3f4f6; background: #fafafa; border-radius: 10px; padding: 18px; }}
        .meta-label {{ font-size: 11px; text-transform: uppercase; color: #6b7280; font-weight: 700; display: block; }}
        .meta-value {{ font-size: 14px; font-weight: 700; color: #111827; margin-top: 2px; }}
        .notice-card {{ background: #fff1f2; border: 1px solid #fecdd3; border-radius: 10px; padding: 16px; margin-bottom: 24px; font-size: 13px; color: #9f1239; line-height: 1.6; }}
        .receipt-footer {{ background: #111827; color: #9ca3af; padding: 22px 28px; text-align: center; font-size: 12px; }}
        .footer-brand {{ color: #ffffff; font-weight: 700; margin-bottom: 4px; }}
    </style>
</head>
<body>
    <div class='receipt-card'>
        <div class='receipt-header'>
            <h1 class='receipt-brand'>Beauty Hair Studio</h1>
            <p class='receipt-title'>Reservation Cancellation Notice</p>
        </div>
        <div class='receipt-body'>
            <div style='text-align: center;'>
                <span class='status-pill-cancelled'>✕ APPOINTMENT CANCELLED</span>
            </div>

            <div class='greeting'>Hello {appointment.ClientName},</div>
            <p class='intro-text'>
                This email confirms that your salon appointment <strong>#APT-{appointment.Id}</strong> has been cancelled. No cancellation fees have been charged.
            </p>

            <div class='meta-grid'>
                <div>
                    <span class='meta-label'>Cancelled Service</span>
                    <div class='meta-value' style='color: #991b1b;'>{appointment.ServiceName}</div>
                </div>
                <div>
                    <span class='meta-label'>Originally Assigned Specialist</span>
                    <div class='meta-value'>{appointment.StylistName}</div>
                </div>
                <div>
                    <span class='meta-label'>Original Date</span>
                    <div class='meta-value'>{appointment.Date:dddd, MMMM dd, yyyy}</div>
                </div>
                <div>
                    <span class='meta-label'>Original Time Slot</span>
                    <div class='meta-value'>{appointment.TimeSlot}</div>
                </div>
                <div>
                    <span class='meta-label'>Reference ID</span>
                    <div class='meta-value' style='font-family: monospace;'>APT-{appointment.Id}</div>
                </div>
                <div>
                    <span class='meta-label'>Cancellation Status</span>
                    <div class='meta-value' style='color: #dc2626;'>Cancelled</div>
                </div>
            </div>");

        if (!string.IsNullOrWhiteSpace(reason))
        {
            sb.Append($@"
            <div style='background: #f3f4f6; border: 1px solid #e5e7eb; border-radius: 8px; padding: 12px 16px; margin-bottom: 20px; font-size: 13px; color: #374151;'>
                <strong>Reason provided:</strong> {reason}
            </div>");
        }

        sb.Append($@"
            <div class='notice-card'>
                <strong>Need to reschedule?</strong> We would love to welcome you back at a time that works better for you. You can browse our package menu and book a new appointment anytime on our website or by contacting our reception desk.
            </div>

            <div style='text-align: center; margin-bottom: 12px;'>
                <a href='https://beautyhairstudio.com/book' style='display: inline-block; background: #111827; color: #ffffff; text-decoration: none; font-weight: 700; padding: 12px 24px; border-radius: 8px; font-size: 14px;'>
                    Book Another Ritual
                </a>
            </div>
        </div>

        <div class='receipt-footer'>
            <div class='footer-brand'>Beauty Hair Studio &bull; Luxury Haircare & Spa Rituals</div>
            <div>Questions? Contact us at +63 917 123 4567 or visit our salon studio.</div>
        </div>
    </div>
</body>
</html>");

        return sb.ToString();
    }
}
