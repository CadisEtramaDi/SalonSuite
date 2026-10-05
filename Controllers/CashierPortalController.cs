using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Hosting;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class CashierPortal : ComponentBase, IDisposable
{
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public StripePaymentService StripeService { get; set; } = default!;
    [Inject] public XenditPaymentService XenditService { get; set; } = default!;
    [Inject] public EmailReceiptService EmailService { get; set; } = default!;
    [Inject] public CloudinaryImageService CloudinaryService { get; set; } = default!;
    [Inject] public IWebHostEnvironment WebHostEnvironment { get; set; } = default!;

    private bool IsAuthorizedCashier =>
        SalonService.CurrentUser.IsLoggedIn &&
        (SalonService.CurrentUser.Role.Contains("Cashier", StringComparison.OrdinalIgnoreCase) ||
         SalonService.CurrentUser.Role.Contains("Front Desk", StringComparison.OrdinalIgnoreCase) ||
         SalonService.CurrentUser.Role.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
         SalonService.CurrentUser.Role.Contains("Owner", StringComparison.OrdinalIgnoreCase) ||
         SalonService.CurrentUser.Role.Contains("Manager", StringComparison.OrdinalIgnoreCase));

    protected override void OnInitialized()
    {
        SalonService.OnChange += HandleDataChanged;
        SalonService.EnsureSeedData();

        if (!IsAuthorizedCashier)
        {
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }
    }

    // Page state
    private string currentTab = "register";
    private int selectedApptId = 0;
    private string billingClientName = "";
    private string billingStylistName = "";
    private string billingServiceName = "";
    private decimal baseServicePrice = 0;
    private HashSet<int> selectedProductIds = new();
    private decimal addonTotal = 0;
    private string selectedDiscountType = "NONE";
    private decimal calculatedDiscount = 0;

    // Promo & Loyalty Fields
    private string promoCodeInput = "";
    private string promoCodeApplied = "";
    private decimal promoDiscount = 0;
    private bool promoApplied = false;
    private string promoMessage = "";
    private CustomerRecord? matchedCustomer = null;
    private bool redeemPoints = false;
    private decimal loyaltyDiscount = 0;

    private decimal netTotalDue = 0;
    private string selectedPaymentMethod = "Cash";
    private decimal cashTendered = 0;
    private decimal calculatedChange = 0;

    // Stripe Cashier Fields
    private string stripeReceiptEmail = "";
    private bool isProcessingStripe = false;
    private string? stripeErrorMessage = null;

    // Xendit GCash Fields
    private string gcashReceiptEmail = "";
    private bool isProcessingGcash = false;
    private string? gcashErrorMessage = null;
    private string gcashReferenceNumber = "";
    private string? activeGcashQrUrl = null;
    private string? activeGcashInvoiceUrl = null;
    private string? activeGcashInvoiceId = null;
    private string? activeGcashExternalId = null;
    private string? gcashCheckNotice = null;
    private bool isCheckingGcashStatus = false;
    private CancellationTokenSource? _gcashPollCts;

    // GCash Receipt Picture Upload & Validation Fields
    private string gcashPaymentMode = "manual"; // "manual" (Counter QR & Proof Upload) or "xendit" (Dynamic Gateway QR)
    private string? gcashReceiptPreviewUrl = null;
    private string? gcashReceiptUploadedUrl = null;
    private string? gcashReceiptFileName = null;
    private long gcashReceiptFileSize = 0;
    private bool isUploadingGcashReceipt = false;
    private string? gcashReceiptUploadError = null;
    private string? gcashValidationMessage = null;
    private bool showGcashReceiptViewerModal = false;
    private string? receiptModalProofImageUrl = null;

    private string? successNotification = null;
    private InvoiceRecord? lastPaidInvoice = null;
    private InvoiceRecord? viewingReceipt = null;
    private string receiptModalEmail = "";
    private bool isSendingReceiptEmail = false;
    private string? receiptModalEmailStatus = null;
    private bool receiptModalEmailSuccess = false;

    // Floor & Chair Monitor Fields
    private string floorStylistFilter = "ALL";
    private string floorSearchQuery = "";
    private bool showQuickSeatModal = false;
    private AppointmentRecord newQuickSeatAppt = new()
    {
        ServiceName = "01 Haircut",
        TimeSlot = "Now (Floor)",
        Price = 850,
        Status = "In Progress"
    };

    // Walk-in form
    private string walkinName = "";
    private string walkinPhone = "";
    private string walkinService = "Precision Haircut";
    private string walkinStylist = "Sofia Martinez";
    private string walkinSlot = "Walk-in (Now)";

    private int ActiveChairsCount => SalonService.Appointments.Count(a => a.Status == "In Progress");

    private bool HasAwaitingPayments => SalonService.Appointments.Any(a => a.Status == "Completed" && !a.IsPaid);

    private AppointmentRecord? SelectedAppointment =>
        selectedApptId > 0
            ? SalonService.Appointments.FirstOrDefault(a => a.Id == selectedApptId && a.Status == "Completed" && !a.IsPaid)
            : null;

    private bool HasSelectedAwaitingPayment => SelectedAppointment != null || !string.IsNullOrWhiteSpace(billingClientName);

    private List<AppointmentRecord> FilteredFloorAppointments
    {
        get
        {
            var list = SalonService.Appointments.AsEnumerable();

            if (floorStylistFilter != "ALL")
            {
                list = list.Where(a => a.StylistName.Contains(floorStylistFilter, StringComparison.OrdinalIgnoreCase) ||
                                       floorStylistFilter.Contains(a.StylistName, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(floorSearchQuery))
            {
                var q = floorSearchQuery.Trim();
                list = list.Where(a => (a.ClientName != null && a.ClientName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                                       (a.ClientPhone != null && a.ClientPhone.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                                       (a.ServiceName != null && a.ServiceName.Contains(q, StringComparison.OrdinalIgnoreCase)));
            }

            return list.OrderByDescending(a => a.Id).ToList();
        }
    }

    [SupplyParameterFromQuery]
    public string? stripe_session_id { get; set; }

    [SupplyParameterFromQuery]
    public string? xendit_id { get; set; }

    [SupplyParameterFromQuery]
    public string? xendit_simulated { get; set; }

    [SupplyParameterFromQuery]
    public string? appt_id { get; set; }

    [SupplyParameterFromQuery]
    public string? status { get; set; }

    protected override async Task OnInitializedAsync()
    {
        SalonService.OnChange += HandleDataChanged;
        SalonService.EnsureSeedData();

        if (!string.IsNullOrWhiteSpace(stripe_session_id))
        {
            await HandleStripeReturnAsync(stripe_session_id);
        }
        else if (!string.IsNullOrWhiteSpace(xendit_id))
        {
            await HandleXenditReturnAsync(xendit_id);
        }

        var firstUnpaid = SalonService.Appointments.FirstOrDefault(a => a.Status == "Completed" && !a.IsPaid);
        if (firstUnpaid != null)
        {
            SelectAppointmentForBilling(firstUnpaid);
        }
        else
        {
            ClearBillingSelection();
        }
    }

    private async Task HandleXenditReturnAsync(string xenditId)
    {
        try
        {
            var result = await XenditService.VerifyInvoiceAsync(xenditId);
            if (result.IsPaid)
            {
                int targetApptId = 0;
                if (!string.IsNullOrWhiteSpace(appt_id))
                {
                    int.TryParse(appt_id, out targetApptId);
                }

                if (targetApptId == 0 && xenditId.Contains("APPT-"))
                {
                    var parts = xenditId.Split('-');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "APPT" && int.TryParse(parts[i + 1], out int id))
                        {
                            targetApptId = id;
                            break;
                        }
                    }
                }

                var appt = SalonService.Appointments.FirstOrDefault(a => a.Id == targetApptId);
                var clientName = appt?.ClientName ?? "GCash Customer";
                var serviceName = appt?.ServiceName ?? "Salon Service";
                var amount = result.Amount > 0 ? result.Amount : (appt?.Price ?? 850);

                var existingInv = SalonService.Invoices.FirstOrDefault(i => i.AppointmentId == targetApptId && targetApptId > 0);
                if (existingInv == null)
                {
                    var invoice = new InvoiceRecord
                    {
                        AppointmentId = targetApptId,
                        CustomerId = appt?.CustomerId,
                        ClientName = clientName,
                        ServiceName = serviceName,
                        StylistName = appt?.StylistName ?? "Sofia Martinez",
                        Subtotal = amount,
                        Total = amount,
                        AmountPaid = amount,
                        PaymentMethod = "GCash (Xendit)",
                        Timestamp = DateTime.UtcNow,
                        CashierName = SalonService.CurrentUser.IsLoggedIn ? SalonService.CurrentUser.Name : "Clara Santos"
                    };

                    SalonService.AddInvoice(invoice);
                    lastPaidInvoice = invoice;
                    SetSuccessNotification($"GCash Payment of ₱{invoice.Total:N0} successfully verified via Xendit for {invoice.ClientName}! (Invoice: {invoice.InvoiceNumber})");

                    var targetEmail = !string.IsNullOrWhiteSpace(result.CustomerEmail) ? result.CustomerEmail : (appt?.ClientEmail ?? matchedCustomer?.Email);
                    if (!string.IsNullOrWhiteSpace(targetEmail))
                    {
                        _ = EmailService.SendInvoiceReceiptAsync(invoice, targetEmail);
                    }
                }
                else
                {
                    lastPaidInvoice = existingInv;
                    SetSuccessNotification($"Invoice {existingInv.InvoiceNumber} payment verified with Xendit GCash.");
                }
            }
        }
        catch (Exception ex)
        {
            SetSuccessNotification($"Xendit payment callback error: {ex.Message}");
        }
    }

    private async Task HandleStripeReturnAsync(string sessionId)
    {
        try
        {
            var result = await StripeService.VerifySessionAsync(sessionId);
            if (result.IsPaid)
            {
                int apptId = 0;
                if (result.Metadata != null && result.Metadata.TryGetValue("AppointmentId", out var metaApptId))
                {
                    int.TryParse(metaApptId, out apptId);
                }

                var appt = SalonService.Appointments.FirstOrDefault(a => a.Id == apptId);
                var clientName = result.Metadata?.GetValueOrDefault("ClientName") ?? appt?.ClientName ?? "Stripe Customer";
                var serviceName = result.Metadata?.GetValueOrDefault("ServiceName") ?? appt?.ServiceName ?? "Salon Service";
                var amount = result.AmountTotal > 0 ? result.AmountTotal : (appt?.Price ?? 850);

                var existingInv = SalonService.Invoices.FirstOrDefault(i => i.AppointmentId == apptId && apptId > 0);
                if (existingInv == null)
                {
                    var invoice = new InvoiceRecord
                    {
                        AppointmentId = apptId,
                        CustomerId = appt?.CustomerId,
                        ClientName = clientName,
                        ServiceName = serviceName,
                        StylistName = appt?.StylistName ?? "Sofia Martinez",
                        Subtotal = amount,
                        Total = amount,
                        AmountPaid = amount,
                        PaymentMethod = "Stripe Card (Sandbox)",
                        Timestamp = DateTime.UtcNow,
                        CashierName = SalonService.CurrentUser.IsLoggedIn ? SalonService.CurrentUser.Name : "Clara Santos"
                    };

                    SalonService.AddInvoice(invoice);
                    lastPaidInvoice = invoice;
                    SetSuccessNotification($"Payment of ₱{invoice.Total:N0} successfully verified via Stripe Checkout for {invoice.ClientName}! (Invoice: {invoice.InvoiceNumber})");

                    var targetEmail = !string.IsNullOrWhiteSpace(result.CustomerEmail) ? result.CustomerEmail : (appt?.ClientEmail ?? matchedCustomer?.Email);
                    if (!string.IsNullOrWhiteSpace(targetEmail))
                    {
                        _ = EmailService.SendInvoiceReceiptAsync(invoice, targetEmail);
                    }
                }
                else
                {
                    lastPaidInvoice = existingInv;
                    SetSuccessNotification($"Invoice {existingInv.InvoiceNumber} payment verified with Stripe Checkout.");
                }
            }
        }
        catch (Exception ex)
        {
            SetSuccessNotification($"Stripe payment callback processed: {ex.Message}");
        }
    }

    private CancellationTokenSource? _notificationCts;

    private void SetSuccessNotification(string message, int durationSeconds = 5)
    {
        successNotification = message;
        _notificationCts?.Cancel();
        _notificationCts = new CancellationTokenSource();
        var token = _notificationCts.Token;

        _ = Task.Delay(durationSeconds * 1000, token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                InvokeAsync(() =>
                {
                    successNotification = null;
                    StateHasChanged();
                });
            }
        }, token);
    }

    private void HandleDataChanged()
    {
        if (!IsAuthorizedCashier)
        {
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }

        if (selectedApptId > 0 && !SalonService.Appointments.Any(a => a.Id == selectedApptId && a.Status == "Completed" && !a.IsPaid))
        {
            var nextUnpaid = SalonService.Appointments.FirstOrDefault(a => a.Status == "Completed" && !a.IsPaid);
            if (nextUnpaid != null)
            {
                SelectAppointmentForBilling(nextUnpaid);
            }
            else
            {
                ClearBillingSelection();
            }
        }
        else if (selectedApptId == 0)
        {
            var firstUnpaid = SalonService.Appointments.FirstOrDefault(a => a.Status == "Completed" && !a.IsPaid);
            if (firstUnpaid != null)
            {
                SelectAppointmentForBilling(firstUnpaid);
            }
        }
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        SalonService.OnChange -= HandleDataChanged;
        _notificationCts?.Cancel();
        _notificationCts?.Dispose();
        _gcashPollCts?.Cancel();
        _gcashPollCts?.Dispose();
    }

    private void OnClientNameChanged()
    {
        matchedCustomer = SalonService.FindCustomerByPhoneOrName(billingClientName);
        redeemPoints = false;
        loyaltyDiscount = 0;
        CalculateTotals();
    }

    private void SelectAppointmentForBilling(AppointmentRecord appt)
    {
        if (appt.IsPaid || appt.Status != "Completed")
        {
            return;
        }

        _gcashPollCts?.Cancel();
        activeGcashQrUrl = null;
        activeGcashInvoiceUrl = null;
        activeGcashInvoiceId = null;
        activeGcashExternalId = null;

        selectedApptId = appt.Id;
        billingClientName = appt.ClientName;
        billingStylistName = appt.StylistName;
        billingServiceName = appt.ServiceName;
        baseServicePrice = appt.Price;
        selectedProductIds.Clear();
        selectedDiscountType = "NONE";
        promoCodeInput = "";
        promoCodeApplied = "";
        promoDiscount = 0;
        promoApplied = false;
        promoMessage = "";
        matchedCustomer = SalonService.FindCustomerByPhoneOrName(appt.ClientName);
        stripeReceiptEmail = appt.ClientEmail ?? matchedCustomer?.Email ?? "";
        stripeErrorMessage = null;
        gcashReceiptEmail = appt.ClientEmail ?? matchedCustomer?.Email ?? "";
        gcashReferenceNumber = "";
        gcashErrorMessage = null;
        gcashValidationMessage = null;
        RemoveGcashReceiptImage();
        redeemPoints = false;
        loyaltyDiscount = 0;
        CalculateTotals();
    }

    private void SendApptToRegister(AppointmentRecord appt)
    {
        SelectAppointmentForBilling(appt);
        currentTab = "register";
    }

    private void ClearBillingSelection()
    {
        _gcashPollCts?.Cancel();
        activeGcashQrUrl = null;
        activeGcashInvoiceUrl = null;
        activeGcashInvoiceId = null;
        activeGcashExternalId = null;

        selectedApptId = 0;
        billingClientName = "";
        billingStylistName = "";
        billingServiceName = "";
        baseServicePrice = 0;
        selectedProductIds.Clear();
        selectedDiscountType = "NONE";
        promoCodeInput = "";
        promoCodeApplied = "";
        promoDiscount = 0;
        promoApplied = false;
        promoMessage = "";
        matchedCustomer = null;
        stripeReceiptEmail = "";
        stripeErrorMessage = null;
        gcashReceiptEmail = "";
        gcashReferenceNumber = "";
        gcashErrorMessage = null;
        gcashValidationMessage = null;
        RemoveGcashReceiptImage();
        redeemPoints = false;
        loyaltyDiscount = 0;
        netTotalDue = 0;
        cashTendered = 0;
        calculatedChange = 0;
    }

    private void ToggleProductSelection(int productId)
    {
        if (selectedProductIds.Contains(productId))
        {
            selectedProductIds.Remove(productId);
        }
        else
        {
            selectedProductIds.Add(productId);
        }
        CalculateTotals();
    }

    private void ToggleRedeemPoints()
    {
        if (matchedCustomer != null && matchedCustomer.LoyaltyPoints >= 200)
        {
            redeemPoints = true;
            loyaltyDiscount = 350;
        }
        else
        {
            redeemPoints = false;
            loyaltyDiscount = 0;
        }
        CalculateTotals();
    }

    private void ApplyPromoCode()
    {
        decimal sub = baseServicePrice + addonTotal;
        var result = SalonService.ValidatePromo(promoCodeInput, sub);
        promoMessage = result.Message;
        promoApplied = result.IsValid;

        if (result.IsValid)
        {
            promoCodeApplied = promoCodeInput.ToUpper().Trim();
            promoDiscount = result.DiscountAmount;
        }
        else
        {
            promoCodeApplied = "";
            promoDiscount = 0;
        }
        CalculateTotals();
    }

    private void RecalculateBasePrice()
    {
        var pkg = SalonService.Packages.FirstOrDefault(p => billingServiceName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (pkg != null)
        {
            baseServicePrice = pkg.Price;
            CalculateTotals();
            return;
        }

        var svc = SalonService.Services.FirstOrDefault(s => billingServiceName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (svc != null)
        {
            baseServicePrice = svc.Price;
            CalculateTotals();
            return;
        }
    }

    private void SetDiscount(string type)
    {
        selectedDiscountType = type;
        CalculateTotals();
    }

    private void CalculateTotals()
    {
        addonTotal = SalonService.Products
            .Where(p => selectedProductIds.Contains(p.Id))
            .Sum(p => p.RetailPrice);

        decimal sub = baseServicePrice + addonTotal;

        calculatedDiscount = selectedDiscountType switch
        {
            "SENIOR" => Math.Round(sub * 0.20m, 2),
            "VIP" => Math.Round(sub * 0.10m, 2),
            _ => 0
        };

        decimal totalDeductions = calculatedDiscount + promoDiscount + loyaltyDiscount;
        netTotalDue = Math.Max(0, sub - totalDeductions);

        if (cashTendered < netTotalDue)
        {
            cashTendered = netTotalDue;
        }
        calculatedChange = Math.Max(0, cashTendered - netTotalDue);
    }

    private void SetExactCash()
    {
        cashTendered = netTotalDue;
        CalculateTotals();
    }

    private void AddTender(decimal amount)
    {
        cashTendered += amount;
        CalculateTotals();
    }

    private void ProcessPayment()
    {
        if (string.IsNullOrWhiteSpace(billingClientName))
        {
            SetSuccessNotification("Cannot process payment: Please enter or select a client.");
            return;
        }

        var purchasedList = selectedProductIds.Select(id => (ProductId: id, Qty: 1)).ToList();

        var invoice = new InvoiceRecord
        {
            AppointmentId = selectedApptId,
            CustomerId = matchedCustomer?.Id ?? SelectedAppointment?.CustomerId,
            ClientName = billingClientName.Trim(),
            ServiceName = (string.IsNullOrWhiteSpace(billingServiceName) ? "Salon Service" : billingServiceName) + (addonTotal > 0 ? $" (+ {selectedProductIds.Count} Retail Items)" : ""),
            StylistName = string.IsNullOrWhiteSpace(billingStylistName) ? "Sofia Martinez" : billingStylistName,
            Subtotal = baseServicePrice,
            RetailAddonsTotal = addonTotal,
            Discount = calculatedDiscount,
            PromoCode = promoApplied ? promoCodeApplied : null,
            PromoDiscount = promoDiscount,
            LoyaltyPointsRedeemed = redeemPoints ? 200 : 0,
            LoyaltyDiscount = loyaltyDiscount,
            Total = netTotalDue,
            AmountPaid = selectedPaymentMethod == "Cash" ? (cashTendered >= netTotalDue ? cashTendered : netTotalDue) : netTotalDue,
            PaymentMethod = selectedPaymentMethod,
            Timestamp = DateTime.UtcNow,
            CashierName = SalonService.CurrentUser.IsLoggedIn ? SalonService.CurrentUser.Name : "Clara Santos"
        };

        SalonService.AddInvoice(invoice, purchasedList);
        lastPaidInvoice = invoice;
        SetSuccessNotification($"Invoice {invoice.InvoiceNumber} created for {invoice.ClientName} (₱{invoice.Total:N0} via {invoice.PaymentMethod}).");

        var clientEmail = !string.IsNullOrWhiteSpace(stripeReceiptEmail) ? stripeReceiptEmail : (SelectedAppointment?.ClientEmail ?? matchedCustomer?.Email);
        if (!string.IsNullOrWhiteSpace(clientEmail))
        {
            _ = EmailService.SendInvoiceReceiptAsync(invoice, clientEmail);
        }

        var nextUnpaid = SalonService.Appointments.FirstOrDefault(a => a.Status == "Completed" && !a.IsPaid);
        if (nextUnpaid != null)
        {
            SelectAppointmentForBilling(nextUnpaid);
        }
        else
        {
            ClearBillingSelection();
        }
    }

    private async Task ProcessStripeCardPayment()
    {
        if (!HasSelectedAwaitingPayment || string.IsNullOrWhiteSpace(billingClientName) || netTotalDue <= 0)
        {
            stripeErrorMessage = "Cannot process payment: No valid client or active ticket is selected.";
            return;
        }

        try
        {
            isProcessingStripe = true;
            stripeErrorMessage = null;
            StateHasChanged();

            var emailToUse = !string.IsNullOrWhiteSpace(stripeReceiptEmail) 
                ? stripeReceiptEmail 
                : (SelectedAppointment?.ClientEmail ?? matchedCustomer?.Email);

            var result = await StripeService.ProcessCashierCardPaymentAsync(
                netTotalDue,
                billingClientName,
                emailToUse,
                billingServiceName
            );

            if (result.Success)
            {
                var purchasedList = selectedProductIds.Select(id => (ProductId: id, Qty: 1)).ToList();

                var invoice = new InvoiceRecord
                {
                    AppointmentId = selectedApptId,
                    CustomerId = matchedCustomer?.Id ?? SelectedAppointment?.CustomerId,
                    ClientName = billingClientName,
                    ServiceName = billingServiceName + (addonTotal > 0 ? $" (+ {selectedProductIds.Count} Retail Items)" : ""),
                    StylistName = billingStylistName,
                    Subtotal = baseServicePrice,
                    RetailAddonsTotal = addonTotal,
                    Discount = calculatedDiscount,
                    PromoCode = promoApplied ? promoCodeApplied : null,
                    PromoDiscount = promoDiscount,
                    LoyaltyPointsRedeemed = redeemPoints ? 200 : 0,
                    LoyaltyDiscount = loyaltyDiscount,
                    Total = netTotalDue,
                    AmountPaid = netTotalDue,
                    PaymentMethod = "Stripe Card (Sandbox)",
                    Timestamp = DateTime.UtcNow,
                    CashierName = SalonService.CurrentUser.IsLoggedIn ? SalonService.CurrentUser.Name : "Clara Santos"
                };

                SalonService.AddInvoice(invoice, purchasedList);
                lastPaidInvoice = invoice;
                SetSuccessNotification($"Invoice {invoice.InvoiceNumber} successfully charged via Stripe Sandbox! (Txn ID: {result.TransactionId} • ₱{invoice.Total:N0})");

                if (!string.IsNullOrWhiteSpace(emailToUse))
                {
                    _ = EmailService.SendInvoiceReceiptAsync(invoice, emailToUse);
                }

                var nextUnpaid = SalonService.Appointments.FirstOrDefault(a => a.Status == "Completed" && !a.IsPaid);
                if (nextUnpaid != null)
                {
                    SelectAppointmentForBilling(nextUnpaid);
                }
                else
                {
                    ClearBillingSelection();
                }
            }
            else
            {
                stripeErrorMessage = result.ErrorMessage ?? "Payment was declined or encountered an error.";
            }
        }
        catch (Exception ex)
        {
            stripeErrorMessage = $"Stripe charge error: {ex.Message}";
        }
        finally
        {
            isProcessingStripe = false;
            StateHasChanged();
        }
    }

    private async Task OpenStripeHostedCheckout()
    {
        if (!HasSelectedAwaitingPayment || string.IsNullOrWhiteSpace(billingClientName) || netTotalDue <= 0)
        {
            stripeErrorMessage = "Cannot proceed: No valid client or active ticket is selected.";
            return;
        }

        try
        {
            isProcessingStripe = true;
            stripeErrorMessage = null;
            StateHasChanged();

            var emailToUse = !string.IsNullOrWhiteSpace(stripeReceiptEmail) 
                ? stripeReceiptEmail 
                : (SelectedAppointment?.ClientEmail ?? matchedCustomer?.Email);

            var checkoutUrl = await StripeService.CreateCashierCheckoutSessionAsync(
                netTotalDue,
                billingClientName,
                emailToUse,
                billingServiceName,
                selectedApptId
            );

            Navigation.NavigateTo(checkoutUrl, forceLoad: true);
        }
        catch (Exception ex)
        {
            stripeErrorMessage = $"Error creating Stripe checkout session: {ex.Message}";
            isProcessingStripe = false;
            StateHasChanged();
        }
    }

    private async Task GenerateGcashQrCode()
    {
        if (!HasSelectedAwaitingPayment || string.IsNullOrWhiteSpace(billingClientName) || netTotalDue <= 0)
        {
            gcashErrorMessage = "Cannot proceed: No valid client or active ticket is selected.";
            return;
        }

        try
        {
            isProcessingGcash = true;
            gcashErrorMessage = null;
            StateHasChanged();

            var emailToUse = !string.IsNullOrWhiteSpace(gcashReceiptEmail)
                ? gcashReceiptEmail
                : (SelectedAppointment?.ClientEmail ?? matchedCustomer?.Email);

            var invoiceResult = await XenditService.CreateGcashInvoiceAsync(
                netTotalDue,
                billingClientName,
                emailToUse,
                billingServiceName,
                selectedApptId,
                returnPath: "cashier"
            );

            if (invoiceResult.Success)
            {
                activeGcashQrUrl = invoiceResult.QrCodeUrl;
                activeGcashInvoiceUrl = invoiceResult.InvoiceUrl;
                activeGcashInvoiceId = invoiceResult.InvoiceId;
                activeGcashExternalId = invoiceResult.ExternalId;
                StartGcashAutoPoll();
            }
            else
            {
                gcashErrorMessage = invoiceResult.ErrorMessage ?? "Failed to create Xendit GCash invoice.";
            }
        }
        catch (Exception ex)
        {
            gcashErrorMessage = $"Error creating GCash QR: {ex.Message}";
        }
        finally
        {
            isProcessingGcash = false;
            StateHasChanged();
        }
    }

    private void StartGcashAutoPoll()
    {
        _gcashPollCts?.Cancel();
        _gcashPollCts = new CancellationTokenSource();
        var token = _gcashPollCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested && !string.IsNullOrWhiteSpace(activeGcashInvoiceId ?? activeGcashExternalId))
            {
                await Task.Delay(4000, token);
                if (token.IsCancellationRequested) break;

                try
                {
                    var idToCheck = activeGcashInvoiceId ?? activeGcashExternalId ?? "";
                    var result = await XenditService.VerifyInvoiceAsync(idToCheck);
                    if (result.IsPaid)
                    {
                        await InvokeAsync(() =>
                        {
                            CompleteGcashTransaction(idToCheck, result.Amount, result.CustomerEmail);
                        });
                        break;
                    }
                }
                catch
                {
                    // Continue polling
                }
            }
        }, token);
    }

    private async Task CheckGcashPaymentStatus()
    {
        if (string.IsNullOrWhiteSpace(activeGcashInvoiceId) && string.IsNullOrWhiteSpace(activeGcashExternalId))
        {
            return;
        }

        try
        {
            isCheckingGcashStatus = true;
            gcashCheckNotice = null;
            StateHasChanged();

            var idToCheck = activeGcashInvoiceId ?? activeGcashExternalId ?? "";
            var result = await XenditService.VerifyInvoiceAsync(idToCheck);

            if (result.IsPaid)
            {
                CompleteGcashTransaction(idToCheck, result.Amount, result.CustomerEmail);
            }
            else
            {
                gcashCheckNotice = $"Status: {result.Status.ToUpper()} — Awaiting customer action on GCash (Checked at {DateTime.Now:hh:mm:ss tt})";
            }
        }
        catch (Exception ex)
        {
            gcashCheckNotice = $"Error checking status: {ex.Message}";
        }
        finally
        {
            isCheckingGcashStatus = false;
            StateHasChanged();
        }
    }

    private void SimulateGcashPayment()
    {
        var idToUse = activeGcashInvoiceId ?? activeGcashExternalId ?? $"xnd_sim_{DateTime.UtcNow.Ticks}";
        CompleteGcashTransaction(idToUse, netTotalDue, gcashReceiptEmail);
    }

    private void CompleteGcashTransaction(string xenditId, decimal paidAmount, string payerEmail)
    {
        _gcashPollCts?.Cancel();
        activeGcashQrUrl = null;
        activeGcashInvoiceUrl = null;
        activeGcashInvoiceId = null;
        activeGcashExternalId = null;

        var purchasedList = selectedProductIds.Select(id => (ProductId: id, Qty: 1)).ToList();

        var invoice = new InvoiceRecord
        {
            AppointmentId = selectedApptId,
            CustomerId = matchedCustomer?.Id ?? SelectedAppointment?.CustomerId,
            ClientName = billingClientName,
            ServiceName = billingServiceName + (addonTotal > 0 ? $" (+ {selectedProductIds.Count} Retail Items)" : ""),
            StylistName = billingStylistName,
            Subtotal = baseServicePrice,
            RetailAddonsTotal = addonTotal,
            Discount = calculatedDiscount,
            PromoCode = promoApplied ? promoCodeApplied : null,
            PromoDiscount = promoDiscount,
            LoyaltyPointsRedeemed = redeemPoints ? 200 : 0,
            LoyaltyDiscount = loyaltyDiscount,
            Total = netTotalDue,
            AmountPaid = netTotalDue,
            PaymentMethod = "GCash (Xendit QR)",
            ReceiptImageUrl = gcashReceiptUploadedUrl ?? gcashReceiptPreviewUrl,
            Timestamp = DateTime.UtcNow,
            CashierName = SalonService.CurrentUser.IsLoggedIn ? SalonService.CurrentUser.Name : "Clara Santos"
        };

        SalonService.AddInvoice(invoice, purchasedList);
        lastPaidInvoice = invoice;
        SetSuccessNotification($"GCash Payment of ₱{invoice.Total:N0} successfully confirmed via Xendit QR for {invoice.ClientName}! (Invoice: {invoice.InvoiceNumber})");

        var targetEmail = !string.IsNullOrWhiteSpace(payerEmail) ? payerEmail : (gcashReceiptEmail);
        if (!string.IsNullOrWhiteSpace(targetEmail))
        {
            _ = EmailService.SendInvoiceReceiptAsync(invoice, targetEmail);
        }

        RemoveGcashReceiptImage();
        gcashValidationMessage = null;

        var nextUnpaid = SalonService.Appointments.FirstOrDefault(a => a.Status == "Completed" && !a.IsPaid);
        if (nextUnpaid != null)
        {
            SelectAppointmentForBilling(nextUnpaid);
        }
        else
        {
            ClearBillingSelection();
        }
    }

    private void CancelGcashQr()
    {
        _gcashPollCts?.Cancel();
        activeGcashQrUrl = null;
        activeGcashInvoiceUrl = null;
        activeGcashInvoiceId = null;
        activeGcashExternalId = null;
        gcashErrorMessage = null;
    }

    private async Task OpenXenditGcashCheckout()
    {
        if (!HasSelectedAwaitingPayment || string.IsNullOrWhiteSpace(billingClientName) || netTotalDue <= 0)
        {
            gcashErrorMessage = "Cannot proceed: No valid client or active ticket is selected.";
            return;
        }

        try
        {
            isProcessingGcash = true;
            gcashErrorMessage = null;
            StateHasChanged();

            var emailToUse = !string.IsNullOrWhiteSpace(gcashReceiptEmail)
                ? gcashReceiptEmail
                : (SelectedAppointment?.ClientEmail ?? matchedCustomer?.Email);

            var invoiceResult = await XenditService.CreateGcashInvoiceAsync(
                netTotalDue,
                billingClientName,
                emailToUse,
                billingServiceName,
                selectedApptId,
                returnPath: "cashier"
            );

            if (invoiceResult.Success && !string.IsNullOrWhiteSpace(invoiceResult.InvoiceUrl))
            {
                Navigation.NavigateTo(invoiceResult.InvoiceUrl, forceLoad: true);
            }
            else
            {
                gcashErrorMessage = invoiceResult.ErrorMessage ?? "Failed to create Xendit GCash invoice.";
                isProcessingGcash = false;
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            gcashErrorMessage = $"Error initiating GCash checkout: {ex.Message}";
            isProcessingGcash = false;
            StateHasChanged();
        }
    }

    private async Task HandleGcashReceiptUpload(InputFileChangeEventArgs e)
    {
        gcashReceiptUploadError = null;
        gcashValidationMessage = null;

        var file = e.File;
        if (file == null) return;

        // 1. Validate File Format (Images only)
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif" };
        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        var isImageMime = !string.IsNullOrEmpty(file.ContentType) && file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
        var isImageExt = allowedExtensions.Contains(ext);

        if (!isImageMime && !isImageExt)
        {
            gcashReceiptUploadError = "Invalid file type. Please upload an image file of the GCash receipt (JPG, PNG, WEBP).";
            return;
        }

        // 2. Validate File Size (Max 5MB)
        const long maxSizeBytes = 5 * 1024 * 1024;
        if (file.Size > maxSizeBytes)
        {
            gcashReceiptUploadError = $"The uploaded picture is too large ({(file.Size / (1024.0 * 1024.0)):N1} MB). Maximum allowed size is 5 MB.";
            return;
        }

        if (file.Size == 0)
        {
            gcashReceiptUploadError = "The selected picture file is empty. Please choose a valid receipt image.";
            return;
        }

        isUploadingGcashReceipt = true;
        StateHasChanged();

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: maxSizeBytes);
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var mime = !string.IsNullOrWhiteSpace(file.ContentType) ? file.ContentType : "image/jpeg";
            gcashReceiptPreviewUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            gcashReceiptFileName = file.Name;
            gcashReceiptFileSize = file.Size;

            string? uploadedUrl = null;

            // 1. Attempt Cloudinary cloud upload
            if (CloudinaryService != null && CloudinaryService.IsConfigured)
            {
                try
                {
                    ms.Position = 0;
                    uploadedUrl = await CloudinaryService.UploadStreamAsync(ms, $"gcash_rcpt_{DateTime.UtcNow.Ticks}_{file.Name}", "salonsuite/gcash_receipts");
                }
                catch
                {
                    uploadedUrl = null;
                }
            }

            // 2. Fallback to local wwwroot or data URI
            if (string.IsNullOrWhiteSpace(uploadedUrl))
            {
                try
                {
                    if (WebHostEnvironment?.WebRootPath != null)
                    {
                        var folder = Path.Combine(WebHostEnvironment.WebRootPath, "uploads", "receipts");
                        Directory.CreateDirectory(folder);
                        var safeName = $"gcash_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N")[..8]}{ext}";
                        var fullPath = Path.Combine(folder, safeName);
                        await File.WriteAllBytesAsync(fullPath, bytes);
                        uploadedUrl = $"/uploads/receipts/{safeName}";
                    }
                }
                catch
                {
                    uploadedUrl = gcashReceiptPreviewUrl;
                }
            }

            gcashReceiptUploadedUrl = uploadedUrl ?? gcashReceiptPreviewUrl;
        }
        catch (Exception ex)
        {
            gcashReceiptUploadError = $"Failed to process picture: {ex.Message}";
        }
        finally
        {
            isUploadingGcashReceipt = false;
            StateHasChanged();
        }
    }

    private void RemoveGcashReceiptImage()
    {
        gcashReceiptPreviewUrl = null;
        gcashReceiptUploadedUrl = null;
        gcashReceiptFileName = null;
        gcashReceiptFileSize = 0;
        gcashReceiptUploadError = null;
    }

    private void OpenProofViewer(string? imageUrl)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            receiptModalProofImageUrl = imageUrl;
            showGcashReceiptViewerModal = true;
        }
    }

    private void CloseProofViewer()
    {
        showGcashReceiptViewerModal = false;
        receiptModalProofImageUrl = null;
    }

    private void ProcessManualGcashPayment()
    {
        gcashValidationMessage = null;

        if (!HasSelectedAwaitingPayment || string.IsNullOrWhiteSpace(billingClientName) || netTotalDue <= 0)
        {
            gcashValidationMessage = "Cannot process payment: No valid client ticket is selected or balance is zero.";
            return;
        }

        // VALIDATION: Picture upload required for manual GCash validation
        if (string.IsNullOrWhiteSpace(gcashReceiptUploadedUrl) && string.IsNullOrWhiteSpace(gcashReceiptPreviewUrl))
        {
            gcashValidationMessage = "Validation Error: Please upload a picture of the GCash transaction receipt or screenshot for payment verification.";
            return;
        }

        // VALIDATION: GCash Reference Number required
        if (string.IsNullOrWhiteSpace(gcashReferenceNumber))
        {
            gcashValidationMessage = "Validation Error: Please enter the GCash Reference Number from the customer receipt.";
            return;
        }

        var purchasedList = selectedProductIds.Select(id => (ProductId: id, Qty: 1)).ToList();
        var proofUrl = gcashReceiptUploadedUrl ?? gcashReceiptPreviewUrl;

        var invoice = new InvoiceRecord
        {
            AppointmentId = selectedApptId,
            CustomerId = matchedCustomer?.Id ?? SelectedAppointment?.CustomerId,
            ClientName = billingClientName,
            ServiceName = billingServiceName + (addonTotal > 0 ? $" (+ {selectedProductIds.Count} Retail Items)" : ""),
            StylistName = billingStylistName,
            Subtotal = baseServicePrice,
            RetailAddonsTotal = addonTotal,
            Discount = calculatedDiscount,
            PromoCode = promoApplied ? promoCodeApplied : null,
            PromoDiscount = promoDiscount,
            LoyaltyPointsRedeemed = redeemPoints ? 200 : 0,
            LoyaltyDiscount = loyaltyDiscount,
            Total = netTotalDue,
            AmountPaid = netTotalDue,
            PaymentMethod = $"GCash (Ref: {gcashReferenceNumber.Trim()})",
            ReceiptImageUrl = proofUrl,
            Timestamp = DateTime.UtcNow,
            CashierName = SalonService.CurrentUser.IsLoggedIn ? SalonService.CurrentUser.Name : "Clara Santos"
        };

        SalonService.AddInvoice(invoice, purchasedList);
        lastPaidInvoice = invoice;
        SetSuccessNotification($"GCash Payment of ₱{invoice.Total:N0} successfully verified & recorded for {invoice.ClientName}! (Invoice: {invoice.InvoiceNumber}, Ref: {gcashReferenceNumber.Trim()})");

        var clientEmail = !string.IsNullOrWhiteSpace(gcashReceiptEmail) ? gcashReceiptEmail : (SelectedAppointment?.ClientEmail ?? matchedCustomer?.Email);
        if (!string.IsNullOrWhiteSpace(clientEmail))
        {
            _ = EmailService.SendInvoiceReceiptAsync(invoice, clientEmail);
        }

        RemoveGcashReceiptImage();
        gcashReferenceNumber = "";
        gcashValidationMessage = null;

        var nextUnpaid = SalonService.Appointments.FirstOrDefault(a => a.Status == "Completed" && !a.IsPaid);
        if (nextUnpaid != null)
        {
            SelectAppointmentForBilling(nextUnpaid);
        }
        else
        {
            ClearBillingSelection();
        }
    }

    private void SaveFloorNote(int apptId, string? notes)
    {
        if (notes != null)
        {
            SalonService.UpdateAppointmentNotes(apptId, notes);
        }
    }

    private void HandleReassignStylist(int apptId, string? stylistName)
    {
        if (!string.IsNullOrWhiteSpace(stylistName))
        {
            SalonService.ReassignAppointmentStylist(apptId, stylistName);
            SetSuccessNotification($"Specialist updated to {stylistName}.");
        }
    }

    private void UpdateAppointmentStatusAndNotify(int apptId, string newStatus, string message)
    {
        SalonService.UpdateAppointmentStatus(apptId, newStatus);
        SetSuccessNotification(message);
    }

    private void OpenQuickSeatModal(string stylistName)
    {
        newQuickSeatAppt = new AppointmentRecord
        {
            ClientName = "",
            ClientPhone = "",
            ServiceName = "01 Haircut",
            StylistName = !string.IsNullOrWhiteSpace(stylistName) ? stylistName : (SalonService.Stylists.FirstOrDefault()?.Name ?? "Sofia Martinez"),
            Date = DateTime.Today,
            TimeSlot = "Floor Seated",
            Price = 850,
            Status = "In Progress",
            IsPaid = false
        };
        showQuickSeatModal = true;
    }

    private void SaveQuickSeat()
    {
        if (string.IsNullOrWhiteSpace(newQuickSeatAppt.ClientName)) return;

        decimal price = 850;
        var pkg = SalonService.Packages.FirstOrDefault(p => newQuickSeatAppt.ServiceName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (pkg != null) price = pkg.Price;
        var svc = SalonService.Services.FirstOrDefault(s => newQuickSeatAppt.ServiceName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (svc != null) price = svc.Price;

        newQuickSeatAppt.Price = price;
        newQuickSeatAppt.Date = DateTime.Today;

        SalonService.AddAppointment(newQuickSeatAppt);
        showQuickSeatModal = false;
        SetSuccessNotification($"{newQuickSeatAppt.ClientName} seated with {newQuickSeatAppt.StylistName}.");
    }

    private void SaveWalkinCheckin()
    {
        if (string.IsNullOrWhiteSpace(walkinName)) return;

        decimal price = 1500;
        var pkg = SalonService.Packages.FirstOrDefault(p => walkinService.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (pkg != null) price = pkg.Price;
        var svc = SalonService.Services.FirstOrDefault(s => walkinService.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (svc != null) price = svc.Price;

        var newAppt = new AppointmentRecord
        {
            ClientName = walkinName,
            ClientPhone = walkinPhone,
            ServiceName = walkinService,
            StylistName = walkinStylist,
            Date = DateTime.Today,
            TimeSlot = walkinSlot,
            Price = price,
            Status = "Confirmed",
            IsPaid = false
        };

        SalonService.AddAppointment(newAppt);
        currentTab = "floor";
        SetSuccessNotification($"Client {walkinName} checked in and assigned to {walkinStylist}.");
        walkinName = "";
    }

    private void OpenReceiptModal(InvoiceRecord inv)
    {
        viewingReceipt = inv;
        var cust = SalonService.FindCustomerByPhoneOrName(inv.ClientName);
        var appt = SalonService.Appointments.FirstOrDefault(a => a.Id == inv.AppointmentId);
        receiptModalEmail = !string.IsNullOrWhiteSpace(stripeReceiptEmail)
            ? stripeReceiptEmail
            : (appt?.ClientEmail ?? cust?.Email ?? "");
        receiptModalEmailStatus = null;
    }

    private async Task SendModalReceiptEmail()
    {
        if (viewingReceipt == null || string.IsNullOrWhiteSpace(receiptModalEmail)) return;

        isSendingReceiptEmail = true;
        receiptModalEmailStatus = null;
        StateHasChanged();

        var result = await EmailService.SendInvoiceReceiptAsync(viewingReceipt, receiptModalEmail);
        isSendingReceiptEmail = false;
        receiptModalEmailSuccess = result.Sent;
        receiptModalEmailStatus = result.Message ?? (result.Sent ? $"Receipt sent to {receiptModalEmail}" : "Failed to send email");
        StateHasChanged();
    }

    private void HandleLogout()
    {
        SalonService.Logout();
        Navigation.NavigateTo("/", forceLoad: true);
    }
}
