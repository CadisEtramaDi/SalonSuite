using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<InvoiceRecord> Invoices { get; private set; } = new();

    private async Task LoadInvoicesFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("invoices").GetSnapshotAsync();
            var list = new List<InvoiceRecord>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<InvoiceRecord>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Invoices = list.OrderByDescending(i => i.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading invoices: {ex.Message}");
        }
    }

    public void AddInvoice(InvoiceRecord invoice, List<(int ProductId, int Qty)>? purchasedProducts = null)
    {
        // 1. Ensure Timestamp is UTC for Firestore compatibility
        if (invoice.Timestamp == default || invoice.Timestamp.Kind != DateTimeKind.Utc)
        {
            invoice.Timestamp = DateTime.UtcNow;
        }

        // 2. Mark Appointment as Paid in database and memory
        var appt = Appointments.FirstOrDefault(a => a.Id == invoice.AppointmentId);
        if (appt != null)
        {
            appt.IsPaid = true;
            appt.Status = "Completed";
            if (appt.Date.Kind != DateTimeKind.Utc)
            {
                appt.Date = DateTime.SpecifyKind(appt.Date, DateTimeKind.Utc);
            }
            RunBackgroundTask(async () => await SaveDocAsync("appointments", appt.Id.ToString(), appt));
        }

        // 3. Decrement inventory for any retail products purchased
        if (purchasedProducts != null)
        {
            foreach (var (productId, qty) in purchasedProducts)
            {
                AdjustProductStock(productId, -qty);
            }
        }

        // 4. Customer CRM Lifetime Spend & Loyalty Points
        var customer = Customers.FirstOrDefault(c => (invoice.CustomerId.HasValue && c.Id == invoice.CustomerId.Value) ||
                                                     (!string.IsNullOrEmpty(c.FullName) && c.FullName.Equals(invoice.ClientName, StringComparison.OrdinalIgnoreCase)));
        if (customer == null && !string.IsNullOrWhiteSpace(invoice.ClientName))
        {
            customer = EnsureCustomer(invoice.ClientName);
            invoice.CustomerId = customer.Id;
        }

        if (customer != null)
        {
            customer.TotalSpent += invoice.Total;
            customer.VisitsCount += 1;
            customer.LastVisit = invoice.Timestamp;
            
            if (invoice.LoyaltyPointsRedeemed > 0)
            {
                customer.LoyaltyPoints = Math.Max(0, customer.LoyaltyPoints - invoice.LoyaltyPointsRedeemed);
            }

            customer.LoyaltyPoints += invoice.PointsEarned;
            customer.Tier = CalculateTier(customer.TotalSpent);

            UpdateCustomer(customer);
        }

        // 5. Update Promo usage count if applicable
        if (!string.IsNullOrWhiteSpace(invoice.PromoCode))
        {
            var promo = Promotions.FirstOrDefault(p => p.Code.Equals(invoice.PromoCode, StringComparison.OrdinalIgnoreCase));
            if (promo != null)
            {
                promo.UsageCount += 1;
                RunBackgroundTask(async () => await SaveDocAsync("promotions", promo.Id.ToString(), promo));
            }
        }

        // 6. Persist Invoice to Firestore
        invoice.Id = Invoices.Count > 0 ? Invoices.Max(i => i.Id) + 1 : 1;
        Invoices.Insert(0, invoice);

        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("invoices", invoice.Id.ToString(), invoice));
    }
}
