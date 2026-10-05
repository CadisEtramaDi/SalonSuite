using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class Portal : ComponentBase, IDisposable
{
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public FirebaseAuthService FirebaseAuth { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

    private bool IsAuthorizedAdmin =>
        SalonService.CurrentUser.IsLoggedIn &&
        (SalonService.CurrentUser.Role.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
         SalonService.CurrentUser.Role.Contains("Owner", StringComparison.OrdinalIgnoreCase) ||
         SalonService.CurrentUser.Role.Contains("Manager", StringComparison.OrdinalIgnoreCase));

    protected override void OnInitialized()
    {
        SalonService.OnChange += HandleDataChanged;
        SalonService.EnsureSeedData();

        if (!IsAuthorizedAdmin)
        {
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }
    }

    private void HandleDataChanged()
    {
        if (!IsAuthorizedAdmin)
        {
            Navigation.NavigateTo("/", forceLoad: true);
            return;
        }
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        SalonService.OnChange -= HandleDataChanged;
    }

    private string currentTab = "calendar";
    private string serviceSubTab = "services";
    private string customerSearchQuery = "";
    private string serviceSearchQuery = "";
    private string serviceCategoryFilter = "all";
    private string apptStatusFilter = "all";
    private string apptStylistFilter = "all";
    private string apptSortOrder = "newest";
    private string apptSearchQuery = "";
    private string apptViewMode = "calendar"; // "table" or "calendar"
    private DateTime calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime? selectedCalendarDate = DateTime.Today;
    private AppointmentRecord? selectedApptDetail = null;
    private string billingMethodFilter = "all";
    private string billingSearchQuery = "";
    private string reportPeriod = "today";

    // Pagination State
    private int apptPage = 1;
    private const int apptPageSize = 8;
    private int custPage = 1;
    private const int custPageSize = 8;
    private int svcPage = 1;
    private const int svcPageSize = 6;
    private int pkgPage = 1;
    private const int pkgPageSize = 4;
    private int staffPage = 1;
    private const int staffPageSize = 6;
    private int prodPage = 1;
    private const int prodPageSize = 8;
    private int supPage = 1;
    private const int supPageSize = 6;
    private int promoPage = 1;
    private const int promoPageSize = 8;
    private int rewardPage = 1;
    private const int rewardPageSize = 4;
    private int billPage = 1;
    private const int billPageSize = 8;

    // Helper methods for safe pagination calculations
    private int TotalPages(int count, int pageSize) => Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));
    private int CurrentSafePage(int page, int totalPages) => Math.Max(1, Math.Min(page, totalPages));

    private int ApptTotalPages => TotalPages(FilteredAppointments.Count(), apptPageSize);
    private IEnumerable<AppointmentRecord> PagedAppointments =>
        FilteredAppointments.Skip((CurrentSafePage(apptPage, ApptTotalPages) - 1) * apptPageSize).Take(apptPageSize);

    private int CustTotalPages => TotalPages(FilteredCustomers.Count(), custPageSize);
    private IEnumerable<CustomerRecord> PagedCustomers =>
        FilteredCustomers.Skip((CurrentSafePage(custPage, CustTotalPages) - 1) * custPageSize).Take(custPageSize);

    private int SvcTotalPages => TotalPages(FilteredAdminServices.Count(), svcPageSize);
    private IEnumerable<ServiceOfferItem> PagedAdminServices =>
        FilteredAdminServices.Skip((CurrentSafePage(svcPage, SvcTotalPages) - 1) * svcPageSize).Take(svcPageSize);

    private int PkgTotalPages => TotalPages(SalonService.Packages.Count, pkgPageSize);
    private IEnumerable<ServicePackageItem> PagedPackages =>
        SalonService.Packages.Skip((CurrentSafePage(pkgPage, PkgTotalPages) - 1) * pkgPageSize).Take(pkgPageSize);

    private int StaffTotalPages => TotalPages(FilteredStaffMembers.Count(), staffPageSize);
    private IEnumerable<TeamMemberItem> PagedStaffMembers =>
        FilteredStaffMembers.Skip((CurrentSafePage(staffPage, StaffTotalPages) - 1) * staffPageSize).Take(staffPageSize);

    private int ProdTotalPages => TotalPages(SalonService.Products.Count, prodPageSize);
    private IEnumerable<ProductItem> PagedProducts =>
        SalonService.Products.OrderByDescending(p => p.Id).Skip((CurrentSafePage(prodPage, ProdTotalPages) - 1) * prodPageSize).Take(prodPageSize);

    private int SupTotalPages => TotalPages(SalonService.Suppliers.Count, supPageSize);
    private IEnumerable<SupplierRecord> PagedSuppliers =>
        SalonService.Suppliers.Skip((CurrentSafePage(supPage, SupTotalPages) - 1) * supPageSize).Take(supPageSize);

    private int PromoTotalPages => TotalPages(SalonService.Promotions.Count, promoPageSize);
    private IEnumerable<PromotionItem> PagedPromotions =>
        SalonService.Promotions.Skip((CurrentSafePage(promoPage, PromoTotalPages) - 1) * promoPageSize).Take(promoPageSize);

    private int RewardTotalPages => TotalPages(SalonService.LoyaltyRewards.Count, rewardPageSize);
    private IEnumerable<LoyaltyRewardItem> PagedLoyaltyRewards =>
        SalonService.LoyaltyRewards.Skip((CurrentSafePage(rewardPage, RewardTotalPages) - 1) * rewardPageSize).Take(rewardPageSize);

    private int BillTotalPages => TotalPages(FilteredInvoices.Count(), billPageSize);
    private IEnumerable<InvoiceRecord> PagedInvoices =>
        FilteredInvoices.Skip((CurrentSafePage(billPage, BillTotalPages) - 1) * billPageSize).Take(billPageSize);

    // Modal Visibility States
    private bool showNewBookingModal = false;
    private bool showCustomerModal = false;
    private bool editingCustomer = false;
    private bool showServiceModal = false;
    private bool showPackageModal = false;
    private bool showNewStaffModal = false;
    private bool showProductModal = false;
    private bool showSupplierModal = false;
    private bool showPromoModal = false;
    private bool showRewardModal = false;

    // Form Models
    private AppointmentRecord newAppt = new()
    {
        ServiceName = "Signature Package",
        StylistName = "Sofia Martinez",
        Date = DateTime.Today,
        TimeSlot = "02:00 PM",
        Price = 3990,
        Status = "Confirmed"
    };

    private CustomerRecord custForm = new();
    private ServiceOfferItem svcForm = new() { Category = "Hair", Price = 850, DurationMinutes = 45 };
    private ServicePackageItem pkgForm = new() { Price = 2500, DurationMinutes = 90 };
    private string pkgFeaturesInput = "";

    private TeamMemberItem newStaff = new()
    {
        Role = "Senior Stylist",
        Specialization = "Precision Haircut & Styling",
        CommissionRate = 0.25m,
        ImageUrl = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=500&auto=format&fit=crop&q=80"
    };
    private string staffEmailInput = "";
    private string staffPasswordInput = "SalonPass123!";
    private bool createLoginAccount = true;
    private bool showStaffPassword = false;
    private bool isCreatingStaffAccount = false;
    private string staffModalError = "";
    private string staffFeedbackMessage = "";
    private string staffSearchQuery = "";
    private ProductItem prodForm = new() { Category = "Retail", CostPrice = 200, RetailPrice = 450, StockQuantity = 10, ReorderLevel = 5, Unit = "Bottle" };
    private SupplierRecord supForm = new() { SuppliedCategory = "Hair Care & Cosmetics", PaymentTerms = "Net 30" };
    private PromotionItem promoForm = new() { DiscountType = "Percentage", DiscountValue = 10, MinSpend = 500 };
    private LoyaltyRewardItem rewardForm = new() { PointsRequired = 200, DiscountValue = 350 };


    private decimal TodayRevenue => SalonService.Invoices
        .Where(i => i.Timestamp.Date == DateTime.Today)
        .Sum(i => i.Total);

    private IEnumerable<CustomerRecord> FilteredCustomers =>
        string.IsNullOrWhiteSpace(customerSearchQuery)
            ? SalonService.Customers
            : SalonService.Customers.Where(c => c.FullName.Contains(customerSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                                c.Phone.Contains(customerSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                                c.Tier.Contains(customerSearchQuery, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<ServiceOfferItem> FilteredAdminServices =>
        SalonService.Services.Where(s =>
            (serviceCategoryFilter == "all" || s.Category.Equals(serviceCategoryFilter, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(serviceSearchQuery) ||
             s.Name.Contains(serviceSearchQuery, StringComparison.OrdinalIgnoreCase) ||
             s.Description.Contains(serviceSearchQuery, StringComparison.OrdinalIgnoreCase) ||
             s.Number.Contains(serviceSearchQuery, StringComparison.OrdinalIgnoreCase)));

    private IEnumerable<AppointmentRecord> FilteredAppointments
    {
        get
        {
            var query = SalonService.Appointments.Where(a =>
                (apptStatusFilter == "all" || a.Status.Equals(apptStatusFilter, StringComparison.OrdinalIgnoreCase)) &&
                (apptStylistFilter == "all" || a.StylistName.Equals(apptStylistFilter, StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(apptSearchQuery) ||
                 a.ClientName.Contains(apptSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                 a.StylistName.Contains(apptSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                 a.ServiceName.Contains(apptSearchQuery, StringComparison.OrdinalIgnoreCase)));

            return apptSortOrder switch
            {
                "time-asc" => query.OrderBy(a => a.Date)
                                   .ThenBy(a => SalonDataService.ParseTimeSlotToMinutes(a.TimeSlot))
                                   .ThenByDescending(a => a.Id),
                "name-asc" => query.OrderBy(a => a.ClientName)
                                   .ThenBy(a => a.Date),
                "name-desc" => query.OrderByDescending(a => a.ClientName)
                                    .ThenByDescending(a => a.Date),
                "oldest" => query.OrderBy(a => a.Id),
                _ => query.OrderByDescending(a => a.Id)
            };
        }
    }

    // Calendar Navigation and Data Methods
    public class CalendarDayInfo
    {
        public DateTime Date { get; set; }
        public bool IsCurrentMonth { get; set; }
        public bool IsToday => Date.Date == DateTime.Today;
        public List<AppointmentRecord> Appointments { get; set; } = new();
    }

    private void PrevCalendarMonth() => calendarMonth = calendarMonth.AddMonths(-1);
    private void NextCalendarMonth() => calendarMonth = calendarMonth.AddMonths(1);
    private void GoToToday()
    {
        calendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        selectedCalendarDate = DateTime.Today;
    }

    private void SelectCalendarDate(DateTime date)
    {
        selectedCalendarDate = date.Date;
    }

    private void OpenApptDetail(AppointmentRecord appt)
    {
        selectedApptDetail = appt;
    }

    private void CloseApptDetail()
    {
        selectedApptDetail = null;
    }

    private void UpdateApptStatusFromCalendar(int id, string newStatus)
    {
        SalonService.UpdateAppointmentStatus(id, newStatus);
        if (selectedApptDetail != null && selectedApptDetail.Id == id)
        {
            selectedApptDetail.Status = newStatus;
        }
    }

    private void ReassignApptStylistFromCalendar(int id, string newStylist)
    {
        SalonService.ReassignAppointmentStylist(id, newStylist);
        if (selectedApptDetail != null && selectedApptDetail.Id == id)
        {
            selectedApptDetail.StylistName = newStylist;
        }
    }

    private List<CalendarDayInfo> GetCalendarGridDays()
    {
        var days = new List<CalendarDayInfo>();
        var firstDayOfMonth = new DateTime(calendarMonth.Year, calendarMonth.Month, 1);
        int daysInMonth = DateTime.DaysInMonth(calendarMonth.Year, calendarMonth.Month);

        int startDayOffset = (int)firstDayOfMonth.DayOfWeek; // 0 = Sunday
        var startDate = firstDayOfMonth.AddDays(-startDayOffset);

        int totalCells = (startDayOffset + daysInMonth <= 35) ? 35 : 42;

        for (int i = 0; i < totalCells; i++)
        {
            var date = startDate.AddDays(i);
            bool isCurrentMonth = date.Month == calendarMonth.Month && date.Year == calendarMonth.Year;

            var appts = FilteredAppointments
                .Where(a => a.Date.Date == date.Date)
                .OrderBy(a => SalonDataService.ParseTimeSlotToMinutes(a.TimeSlot))
                .ToList();

            days.Add(new CalendarDayInfo
            {
                Date = date,
                IsCurrentMonth = isCurrentMonth,
                Appointments = appts
            });
        }

        return days;
    }

    private IEnumerable<InvoiceRecord> FilteredInvoices =>
        SalonService.Invoices.Where(i =>
            (billingMethodFilter == "all" || i.PaymentMethod.Equals(billingMethodFilter, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(billingSearchQuery) ||
             i.InvoiceNumber.Contains(billingSearchQuery, StringComparison.OrdinalIgnoreCase) ||
             i.ClientName.Contains(billingSearchQuery, StringComparison.OrdinalIgnoreCase) ||
             i.ServiceName.Contains(billingSearchQuery, StringComparison.OrdinalIgnoreCase) ||
             i.StylistName.Contains(billingSearchQuery, StringComparison.OrdinalIgnoreCase)))
        .OrderByDescending(i => i.Id);

    private IEnumerable<InvoiceRecord> ReportInvoices => reportPeriod switch
    {
        "today" => SalonService.Invoices.Where(i => i.Timestamp.Date == DateTime.Today),
        "week" => SalonService.Invoices.Where(i => i.Timestamp >= DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek)),
        "month" => SalonService.Invoices.Where(i => i.Timestamp.Month == DateTime.Today.Month && i.Timestamp.Year == DateTime.Today.Year),
        _ => SalonService.Invoices
    };

    private decimal GetStylistCommissionRate(string name)
    {
        var stylist = SalonService.Team.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return stylist?.CommissionRate ?? 0.25m;
    }

    private void HandleLogout()
    {
        SalonService.Logout();
        Navigation.NavigateTo("/", forceLoad: true);
    }

    private void OnCRMServiceChanged()
    {
        var pkg = SalonService.Packages.FirstOrDefault(p => newAppt.ServiceName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (pkg != null)
        {
            newAppt.Price = pkg.Price;
            return;
        }

        var svc = SalonService.Services.FirstOrDefault(s => newAppt.ServiceName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (svc != null)
        {
            newAppt.Price = svc.Price;
            return;
        }
    }

    private void SaveCRMBooking()
    {
        if (string.IsNullOrWhiteSpace(newAppt.ClientName)) return;

        if (string.IsNullOrWhiteSpace(newAppt.StylistName) ||
            newAppt.StylistName.Contains("Any", StringComparison.OrdinalIgnoreCase))
        {
            newAppt.StylistName = SalonService.FindAvailableStylist(newAppt.Date, newAppt.TimeSlot);
        }

        SalonService.AddAppointment(newAppt);
        showNewBookingModal = false;
        newAppt = new()
        {
            ServiceName = "Signature Package",
            StylistName = "",
            Date = DateTime.Today,
            TimeSlot = "02:00 PM",
            Price = 3990,
            Status = "Confirmed"
        };
    }

    private void EditCustomer(CustomerRecord customer)
    {
        custForm = new CustomerRecord
        {
            Id = customer.Id,
            FullName = customer.FullName,
            Phone = customer.Phone,
            Email = customer.Email,
            Notes = customer.Notes,
            LoyaltyPoints = customer.LoyaltyPoints,
            Tier = customer.Tier,
            TotalSpent = customer.TotalSpent,
            VisitsCount = customer.VisitsCount
        };
        editingCustomer = true;
        showCustomerModal = true;
    }

    private void SaveCustomer()
    {
        if (string.IsNullOrWhiteSpace(custForm.FullName)) return;
        if (editingCustomer)
        {
            SalonService.UpdateCustomer(custForm);
        }
        else
        {
            SalonService.AddCustomer(custForm);
        }
        showCustomerModal = false;
        custForm = new();
        editingCustomer = false;
    }

    private void SaveService()
    {
        if (string.IsNullOrWhiteSpace(svcForm.Name)) return;
        SalonService.AddService(svcForm);
        showServiceModal = false;
        svcForm = new() { Category = "Hair", Price = 850, DurationMinutes = 45 };
    }

    private void SavePackage()
    {
        if (string.IsNullOrWhiteSpace(pkgForm.Name)) return;
        if (!string.IsNullOrWhiteSpace(pkgFeaturesInput))
        {
            pkgForm.Features = pkgFeaturesInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        SalonService.AddPackage(pkgForm);
        showPackageModal = false;
        pkgForm = new() { Price = 2500, DurationMinutes = 90 };
        pkgFeaturesInput = "";
    }

    private IEnumerable<TeamMemberItem> FilteredStaffMembers =>
        string.IsNullOrWhiteSpace(staffSearchQuery)
            ? SalonService.Team
            : SalonService.Team.Where(t => t.Name.Contains(staffSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                           t.Role.Contains(staffSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                           t.Email.Contains(staffSearchQuery, StringComparison.OrdinalIgnoreCase) ||
                                           t.Specialization.Contains(staffSearchQuery, StringComparison.OrdinalIgnoreCase));

    private void OpenNewStaffModal()
    {
        staffModalError = "";
        staffEmailInput = "";
        staffPasswordInput = "SalonPass123!";
        showStaffPassword = false;
        createLoginAccount = true;
        isCreatingStaffAccount = false;

        newStaff = new()
        {
            Role = "Senior Stylist",
            Specialization = "Precision Haircut & Styling",
            CommissionRate = 0.25m,
            ImageUrl = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=500&auto=format&fit=crop&q=80"
        };
        showNewStaffModal = true;
    }

    private void GenerateRandomPassword()
    {
        string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$";
        var random = new Random();
        staffPasswordInput = "Salon" + new string(Enumerable.Repeat(chars, 4).Select(s => s[random.Next(s.Length)]).ToArray()) + "!";
    }

    private async Task SaveNewStaff()
    {
        staffModalError = "";
        if (string.IsNullOrWhiteSpace(newStaff.Name))
        {
            staffModalError = "Please enter the staff member's full name.";
            return;
        }

        if (createLoginAccount)
        {
            if (string.IsNullOrWhiteSpace(staffEmailInput) || !staffEmailInput.Contains("@"))
            {
                staffModalError = "Please enter a valid work email address for login.";
                return;
            }

            if (string.IsNullOrWhiteSpace(staffPasswordInput) || staffPasswordInput.Length < 6)
            {
                staffModalError = "Initial password must be at least 6 characters.";
                return;
            }

            newStaff.Email = staffEmailInput.Trim();

            if (FirebaseAuth.IsConfigured)
            {
                isCreatingStaffAccount = true;
                var result = await FirebaseAuth.SignUpWithEmailPasswordAsync(newStaff.Email, staffPasswordInput);
                isCreatingStaffAccount = false;

                if (!result.Success && !result.ErrorMessage!.Contains("EMAIL_EXISTS"))
                {
                    staffModalError = result.ErrorMessage;
                    return;
                }
            }
        }
        else if (!string.IsNullOrWhiteSpace(staffEmailInput))
        {
            newStaff.Email = staffEmailInput.Trim();
        }

        if (string.IsNullOrWhiteSpace(newStaff.ImageUrl))
        {
            newStaff.ImageUrl = newStaff.Role.Contains("Cashier")
                ? "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=500&auto=format&fit=crop&q=80"
                : "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=500&auto=format&fit=crop&q=80";
        }

        SalonService.AddTeamMember(newStaff);
        showNewStaffModal = false;

        string targetPortal = newStaff.Role.Contains("Cashier") ? "Cashier POS (/cashier)" :
                              newStaff.Role.Contains("Manager") ? "Management ERP (/admin)" : "Stylist Suite (/employee)";

        staffFeedbackMessage = $"Staff member '{newStaff.Name}' successfully provisioned! Credentials ready for {newStaff.Email} (Assigned to {targetPortal}).";
    }

    private async Task HandleResetStaffPassword(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return;

        if (FirebaseAuth.IsConfigured)
        {
            var res = await FirebaseAuth.SendPasswordResetEmailAsync(email);
            if (res.Success)
            {
                staffFeedbackMessage = $"Password reset email sent to {email}.";
            }
            else
            {
                staffFeedbackMessage = res.ErrorMessage ?? "Could not send password reset.";
            }
        }
        else
        {
            staffFeedbackMessage = $"Password reset request recorded for {email}. (Add Firebase API Key in appsettings.json to deliver live reset emails).";
        }
    }

    private void SaveProduct()
    {
        if (string.IsNullOrWhiteSpace(prodForm.Name)) return;
        SalonService.AddProduct(prodForm);
        showProductModal = false;
        prodForm = new() { Category = "Retail", CostPrice = 200, RetailPrice = 450, StockQuantity = 10, ReorderLevel = 5, Unit = "Bottle" };
    }

    private void SaveSupplier()
    {
        if (string.IsNullOrWhiteSpace(supForm.CompanyName)) return;
        SalonService.AddSupplier(supForm);
        showSupplierModal = false;
        supForm = new() { SuppliedCategory = "Hair Care & Cosmetics", PaymentTerms = "Net 30" };
    }

    private void SavePromo()
    {
        if (string.IsNullOrWhiteSpace(promoForm.Code)) return;
        promoForm.Code = promoForm.Code.ToUpper().Trim();
        SalonService.AddPromotion(promoForm);
        showPromoModal = false;
        promoForm = new() { DiscountType = "Percentage", DiscountValue = 10, MinSpend = 500 };
    }

    private void SaveReward()
    {
        if (string.IsNullOrWhiteSpace(rewardForm.Title)) return;
        SalonService.AddLoyaltyReward(rewardForm);
        showRewardModal = false;
        rewardForm = new() { PointsRequired = 200, DiscountValue = 350 };
    }

    private void PrintReport()
    {
        Navigation.NavigateTo("javascript:window.print()");
    }

    private async Task SwitchTab(string tab)
    {
        currentTab = tab;
        if (tab == "reports")
        {
            await Task.Delay(120);
            await RenderAllChartsAsync();
        }
    }

    private async Task SetReportPeriod(string period)
    {
        reportPeriod = period;
        await Task.Delay(80);
        await RenderAllChartsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && currentTab == "reports")
        {
            await Task.Delay(150);
            await RenderAllChartsAsync();
        }
    }

    private async Task RenderAllChartsAsync()
    {
        try
        {
            var invoices = ReportInvoices.ToList();

            // 1. Revenue Velocity / Sales Trend Data
            var revLabels = new List<string>();
            var revServices = new List<decimal>();
            var revRetail = new List<decimal>();
            var revTotal = new List<decimal>();

            if (reportPeriod == "today")
            {
                var timeSlots = new[] { "09 AM", "11 AM", "01 PM", "03 PM", "05 PM", "07 PM" };
                foreach (var slot in timeSlots)
                {
                    revLabels.Add(slot);
                    var matching = invoices.Where(i => i.Timestamp.ToString("hh tt").StartsWith(slot.Split(' ')[0], StringComparison.OrdinalIgnoreCase)).ToList();
                    decimal sTotal = matching.Sum(i => i.Subtotal);
                    decimal rTotal = matching.Sum(i => i.RetailAddonsTotal);
                    revServices.Add(sTotal);
                    revRetail.Add(rTotal);
                    revTotal.Add(sTotal + rTotal);
                }
                if (revTotal.Sum() == 0 && invoices.Count > 0)
                {
                    revLabels.Clear(); revServices.Clear(); revRetail.Clear(); revTotal.Clear();
                    foreach (var inv in invoices)
                    {
                        revLabels.Add(inv.Timestamp.ToString("hh:mm tt"));
                        revServices.Add(inv.Subtotal);
                        revRetail.Add(inv.RetailAddonsTotal);
                        revTotal.Add(inv.Total);
                    }
                }
            }
            else if (reportPeriod == "week")
            {
                for (int d = 6; d >= 0; d--)
                {
                    var day = DateTime.Today.AddDays(-d);
                    revLabels.Add(day.ToString("ddd (MMM dd)"));
                    var matching = invoices.Where(i => i.Timestamp.Date == day.Date).ToList();
                    decimal sTotal = matching.Sum(i => i.Subtotal);
                    decimal rTotal = matching.Sum(i => i.RetailAddonsTotal);
                    revServices.Add(sTotal);
                    revRetail.Add(rTotal);
                    revTotal.Add(sTotal + rTotal);
                }
            }
            else
            {
                var grouped = invoices
                    .GroupBy(i => i.Timestamp.Date)
                    .OrderBy(g => g.Key)
                    .ToList();

                if (grouped.Count > 0)
                {
                    foreach (var g in grouped)
                    {
                        revLabels.Add(g.Key.ToString("MMM dd"));
                        decimal sTotal = g.Sum(i => i.Subtotal);
                        decimal rTotal = g.Sum(i => i.RetailAddonsTotal);
                        revServices.Add(sTotal);
                        revRetail.Add(rTotal);
                        revTotal.Add(sTotal + rTotal);
                    }
                }
                else
                {
                    for (int d = 6; d >= 0; d--)
                    {
                        var day = DateTime.Today.AddDays(-d);
                        revLabels.Add(day.ToString("MMM dd"));
                        revServices.Add(0);
                        revRetail.Add(0);
                        revTotal.Add(0);
                    }
                }
            }

            await JSRuntime.InvokeVoidAsync("SalonCharts.renderRevenueTrend", "revenueTrendCanvas", revLabels, revServices, revRetail, revTotal);

            // 2. Category Donut Chart
            decimal hairSales = invoices.Where(i => i.ServiceName.Contains("Haircut", StringComparison.OrdinalIgnoreCase) || i.ServiceName.Contains("Blowdry", StringComparison.OrdinalIgnoreCase) || i.ServiceName.Contains("Cut", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Subtotal);
            decimal colorSales = invoices.Where(i => i.ServiceName.Contains("Balayage", StringComparison.OrdinalIgnoreCase) || i.ServiceName.Contains("Color", StringComparison.OrdinalIgnoreCase) || i.ServiceName.Contains("Gloss", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Subtotal);
            decimal spaSales = invoices.Where(i => i.ServiceName.Contains("Spa", StringComparison.OrdinalIgnoreCase) || i.ServiceName.Contains("Treatment", StringComparison.OrdinalIgnoreCase) || i.ServiceName.Contains("Scalp", StringComparison.OrdinalIgnoreCase) || i.ServiceName.Contains("Keratin", StringComparison.OrdinalIgnoreCase)).Sum(i => i.Subtotal);
            decimal retailSales = invoices.Sum(i => i.RetailAddonsTotal);
            decimal otherServices = Math.Max(0, invoices.Sum(i => i.Subtotal) - (hairSales + colorSales + spaSales));

            var catLabels = new List<string> { "Haircut & Styling", "Color & Balayage", "Spa & Rituals", "Retail Add-ons" };
            var catData = new List<decimal> { hairSales, colorSales, spaSales, retailSales };
            if (otherServices > 0)
            {
                catLabels.Add("Other Services");
                catData.Add(otherServices);
            }
            if (catData.Sum() == 0)
            {
                catData = new List<decimal> { 3500, 7500, 4200, 1800 };
            }
            var catColors = new List<string> { "#1E293B", "#D97706", "#10B981", "#8B5CF6", "#3B82F6" };

            await JSRuntime.InvokeVoidAsync("SalonCharts.renderCategoryDonut", "categoryDonutCanvas", catLabels, catData, catColors);

            // 3. Specialist Production Bar Chart
            var specLabels = new List<string>();
            var specSales = new List<decimal>();
            var specComm = new List<decimal>();
            foreach (var stylist in SalonService.Team)
            {
                specLabels.Add(stylist.Name);
                var stylistInvs = invoices.Where(i => i.StylistName == stylist.Name).ToList();
                decimal g = stylistInvs.Sum(i => i.Total);
                decimal c = Math.Round(stylistInvs.Sum(i => i.Subtotal) * stylist.CommissionRate, 0);
                specSales.Add(g);
                specComm.Add(c);
            }
            await JSRuntime.InvokeVoidAsync("SalonCharts.renderSpecialistBar", "specialistBarCanvas", specLabels, specSales, specComm);
        }
        catch { }
    }
}
