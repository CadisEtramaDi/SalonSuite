using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class BookingSuccess : ComponentBase, IDisposable
{
    [Inject] public StripePaymentService StripeService { get; set; } = default!;
    [Inject] public XenditPaymentService XenditService { get; set; } = default!;
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public EmailReceiptService EmailService { get; set; } = default!;

    [SupplyParameterFromQuery]
    public string? session_id { get; set; }

    [SupplyParameterFromQuery]
    public string? xendit_id { get; set; }

    [SupplyParameterFromQuery]
    public string? appt_id { get; set; }

    [SupplyParameterFromQuery]
    public string? simulated { get; set; }

    private bool isVerifying = true;
    private bool isSuccess = false;
    private bool isSimulated = false;
    private string? errorMessage;
    private AppointmentRecord? matchedAppt;

    private bool showCancelModal = false;
    private bool isCancelling = false;
    private string cancelReason = "Change of schedule";
    private string? cancelSuccessFeedback;

    protected override async Task OnInitializedAsync()
    {
        SalonService.OnChange += HandleDataChanged;
        SalonService.EnsureSeedData();

        isSimulated = simulated?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

        await ProcessSessionVerificationAsync();
    }

    private async Task ProcessSessionVerificationAsync()
    {
        isVerifying = true;
        try
        {
            int targetApptId = 0;
            if (!string.IsNullOrWhiteSpace(appt_id) && int.TryParse(appt_id, out int parsedId))
            {
                targetApptId = parsedId;
            }

            if (!string.IsNullOrWhiteSpace(session_id))
            {
                var result = await StripeService.VerifySessionAsync(session_id, targetApptId);

                if (result.IsPaid)
                {
                    // Find appointment by ID or session metadata
                    if (targetApptId == 0 && result.Metadata != null && result.Metadata.TryGetValue("AppointmentId", out var metaApptId))
                    {
                        int.TryParse(metaApptId, out targetApptId);
                    }

                    if (targetApptId > 0)
                    {
                        matchedAppt = SalonService.Appointments.FirstOrDefault(a => a.Id == targetApptId);
                    }

                    if (matchedAppt == null && SalonService.Appointments.Count > 0)
                    {
                        matchedAppt = SalonService.Appointments.OrderByDescending(a => a.Id).FirstOrDefault();
                    }

                    if (matchedAppt != null)
                    {
                        // 1. Mark appointment as paid
                        matchedAppt.IsPaid = true;
                        matchedAppt.Status = "Confirmed";
                        SalonService.UpdateAppointmentStatus(matchedAppt.Id, "Confirmed");

                        // 2. Generate and store InvoiceRecord if not already generated
                        var existingInvoice = SalonService.Invoices.FirstOrDefault(i => i.AppointmentId == matchedAppt.Id);
                        if (existingInvoice == null)
                        {
                            var newInvoice = new InvoiceRecord
                            {
                                AppointmentId = matchedAppt.Id,
                                CustomerId = matchedAppt.CustomerId,
                                ClientName = matchedAppt.ClientName,
                                ServiceName = matchedAppt.ServiceName,
                                StylistName = matchedAppt.StylistName,
                                Subtotal = matchedAppt.Price,
                                Total = matchedAppt.Price,
                                AmountPaid = matchedAppt.Price,
                                PaymentMethod = "Stripe Card (Online)",
                                Timestamp = DateTime.Now,
                                CashierName = "Stripe Online Gateway"
                            };

                            SalonService.AddInvoice(newInvoice);
                        }

                        isSuccess = true;
                    }
                    else
                    {
                        isSuccess = true; // Fallback display
                    }
                }
                else
                {
                    errorMessage = "Payment session was not marked as completed by Stripe.";
                }
            }
            else if (!string.IsNullOrWhiteSpace(xendit_id))
            {
                var result = await XenditService.VerifyInvoiceAsync(xendit_id);

                if (result.IsPaid)
                {
                    if (targetApptId == 0 && xendit_id.Contains("APPT-"))
                    {
                        var parts = xendit_id.Split('-');
                        for (int i = 0; i < parts.Length - 1; i++)
                        {
                            if (parts[i] == "APPT" && int.TryParse(parts[i + 1], out int id))
                            {
                                targetApptId = id;
                                break;
                            }
                        }
                    }

                    if (targetApptId > 0)
                    {
                        matchedAppt = SalonService.Appointments.FirstOrDefault(a => a.Id == targetApptId);
                    }

                    if (matchedAppt == null && SalonService.Appointments.Count > 0)
                    {
                        matchedAppt = SalonService.Appointments.OrderByDescending(a => a.Id).FirstOrDefault();
                    }

                    if (matchedAppt != null)
                    {
                        matchedAppt.IsPaid = true;
                        matchedAppt.Status = "Confirmed";
                        SalonService.UpdateAppointmentStatus(matchedAppt.Id, "Confirmed");

                        var existingInvoice = SalonService.Invoices.FirstOrDefault(i => i.AppointmentId == matchedAppt.Id);
                        if (existingInvoice == null)
                        {
                            var newInvoice = new InvoiceRecord
                            {
                                AppointmentId = matchedAppt.Id,
                                CustomerId = matchedAppt.CustomerId,
                                ClientName = matchedAppt.ClientName,
                                ServiceName = matchedAppt.ServiceName,
                                StylistName = matchedAppt.StylistName,
                                Subtotal = matchedAppt.Price,
                                Total = matchedAppt.Price,
                                AmountPaid = matchedAppt.Price,
                                PaymentMethod = "GCash (Xendit Online)",
                                Timestamp = DateTime.Now,
                                CashierName = "Xendit GCash Gateway"
                            };

                            SalonService.AddInvoice(newInvoice);
                        }

                        isSuccess = true;
                    }
                    else
                    {
                        isSuccess = true;
                    }
                }
                else
                {
                    errorMessage = $"GCash payment status is {result.Status}.";
                }
            }
            else if (targetApptId > 0)
            {
                matchedAppt = SalonService.Appointments.FirstOrDefault(a => a.Id == targetApptId);
                if (matchedAppt != null)
                {
                    isSuccess = true;
                }
            }
            else
            {
                errorMessage = "No checkout session ID was provided.";
            }
        }
        catch (Exception ex)
        {
            errorMessage = $"Error processing payment verification: {ex.Message}";
        }
        finally
        {
            isVerifying = false;
        }
    }

    private void PromptCancelReceiptAppt()
    {
        showCancelModal = true;
    }

    private void AbortCancelReceiptAppt()
    {
        showCancelModal = false;
    }

    private async Task ConfirmCancelReceiptAppointment()
    {
        if (matchedAppt == null) return;
        isCancelling = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(cancelReason))
            {
                var reasonTag = $"[Cancelled by Client: {cancelReason}]";
                var updatedNotes = string.IsNullOrWhiteSpace(matchedAppt.Notes)
                    ? reasonTag
                    : $"{matchedAppt.Notes} | {reasonTag}";
                SalonService.UpdateAppointmentNotes(matchedAppt.Id, updatedNotes);
            }

            SalonService.UpdateAppointmentStatus(matchedAppt.Id, "Cancelled");
            matchedAppt.Status = "Cancelled";

            if (!string.IsNullOrWhiteSpace(matchedAppt.ClientEmail))
            {
                _ = EmailService.SendBookingCancellationAsync(matchedAppt, matchedAppt.ClientEmail, cancelReason);
            }

            cancelSuccessFeedback = $"Appointment #APT-{matchedAppt.Id:D5} has been cancelled. Our reception desk will process your refund.";
            showCancelModal = false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error cancelling appointment: {ex.Message}";
        }
        finally
        {
            isCancelling = false;
            StateHasChanged();
        }
    }

    private void HandleDataChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        SalonService.OnChange -= HandleDataChanged;
    }
}
