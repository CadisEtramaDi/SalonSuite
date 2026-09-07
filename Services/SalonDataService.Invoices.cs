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

    private void LoadInvoicesFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, AppointmentId, CustomerId, InvoiceNumber, ClientName, ServiceName, StylistName, Subtotal, RetailAddonsTotal, Discount, PromoCode, PromoDiscount, LoyaltyPointsRedeemed, LoyaltyDiscount, Total, AmountPaid, Change, PaymentMethod, Timestamp, CashierName FROM dbo.Invoices ORDER BY Id DESC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<InvoiceRecord>();
            while (reader.Read())
            {
                list.Add(new InvoiceRecord
                {
                    Id = reader.GetInt32(0),
                    AppointmentId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                    CustomerId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    ClientName = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    ServiceName = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    StylistName = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Subtotal = reader.IsDBNull(7) ? 0m : reader.GetDecimal(7),
                    RetailAddonsTotal = reader.IsDBNull(8) ? 0m : reader.GetDecimal(8),
                    Discount = reader.IsDBNull(9) ? 0m : reader.GetDecimal(9),
                    PromoCode = reader.IsDBNull(10) ? null : reader.GetString(10),
                    PromoDiscount = reader.IsDBNull(11) ? 0m : reader.GetDecimal(11),
                    LoyaltyPointsRedeemed = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    LoyaltyDiscount = reader.IsDBNull(13) ? 0m : reader.GetDecimal(13),
                    Total = reader.IsDBNull(14) ? 0m : reader.GetDecimal(14),
                    AmountPaid = reader.IsDBNull(15) ? 0m : reader.GetDecimal(15),
                    PaymentMethod = reader.IsDBNull(17) ? "Cash" : reader.GetString(17),
                    Timestamp = reader.IsDBNull(18) ? DateTime.Now : reader.GetDateTime(18),
                    CashierName = reader.IsDBNull(19) ? "Front Desk" : reader.GetString(19)
                });
            }
            if (list.Count > 0) Invoices = list;
        }
        catch { }
    }

    public void AddInvoice(InvoiceRecord invoice, List<(int ProductId, int Qty)>? purchasedProducts = null)
    {
        // 1. Mark Appointment as Paid in database and memory
        var appt = Appointments.FirstOrDefault(a => a.Id == invoice.AppointmentId);
        if (appt != null)
        {
            appt.IsPaid = true;
            appt.Status = "Completed";
            ExecuteSqlNonQuery("UPDATE dbo.Appointments SET IsPaid = 1, Status = 'Completed' WHERE Id = @Id;", new SqlParameter("@Id", appt.Id));
        }

        // 2. Decrement inventory for any retail products purchased
        if (purchasedProducts != null)
        {
            foreach (var (productId, qty) in purchasedProducts)
            {
                AdjustProductStock(productId, -qty);
            }
        }

        // 3. Customer CRM Lifetime Spend & Loyalty Points
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

        // 4. Update Promo usage count if applicable
        if (!string.IsNullOrWhiteSpace(invoice.PromoCode))
        {
            var promo = Promotions.FirstOrDefault(p => p.Code.Equals(invoice.PromoCode, StringComparison.OrdinalIgnoreCase));
            if (promo != null)
            {
                promo.UsageCount += 1;
                ExecuteSqlNonQuery("UPDATE dbo.Promotions SET UsageCount = UsageCount + 1 WHERE Id = @Id;", new SqlParameter("@Id", promo.Id));
            }
        }

        // 5. Persist Invoice to SQL Server database
        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.Invoices (AppointmentId, CustomerId, InvoiceNumber, ClientName, ServiceName, StylistName, Subtotal, RetailAddonsTotal, Discount, PromoCode, PromoDiscount, LoyaltyPointsRedeemed, LoyaltyDiscount, Total, AmountPaid, Change, PaymentMethod, Timestamp, CashierName) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@AppointmentId, @CustomerId, @InvoiceNumber, @ClientName, @ServiceName, @StylistName, @Subtotal, @RetailAddonsTotal, @Discount, @PromoCode, @PromoDiscount, @LoyaltyPointsRedeemed, @LoyaltyDiscount, @Total, @AmountPaid, @Change, @PaymentMethod, @Timestamp, @CashierName);",
            new SqlParameter("@AppointmentId", invoice.AppointmentId),
            new SqlParameter("@CustomerId", (object?)invoice.CustomerId ?? DBNull.Value),
            new SqlParameter("@InvoiceNumber", invoice.InvoiceNumber),
            new SqlParameter("@ClientName", invoice.ClientName),
            new SqlParameter("@ServiceName", invoice.ServiceName),
            new SqlParameter("@StylistName", invoice.StylistName),
            new SqlParameter("@Subtotal", invoice.Subtotal),
            new SqlParameter("@RetailAddonsTotal", invoice.RetailAddonsTotal),
            new SqlParameter("@Discount", invoice.Discount),
            new SqlParameter("@PromoCode", (object?)invoice.PromoCode ?? DBNull.Value),
            new SqlParameter("@PromoDiscount", invoice.PromoDiscount),
            new SqlParameter("@LoyaltyPointsRedeemed", invoice.LoyaltyPointsRedeemed),
            new SqlParameter("@LoyaltyDiscount", invoice.LoyaltyDiscount),
            new SqlParameter("@Total", invoice.Total),
            new SqlParameter("@AmountPaid", invoice.AmountPaid),
            new SqlParameter("@Change", invoice.Change),
            new SqlParameter("@PaymentMethod", invoice.PaymentMethod),
            new SqlParameter("@Timestamp", invoice.Timestamp),
            new SqlParameter("@CashierName", invoice.CashierName)
        );

        invoice.Id = dbId > 0 ? dbId : (Invoices.Count > 0 ? Invoices.Max(i => i.Id) + 1 : 1);
        Invoices.Add(invoice);

        NotifyStateChanged();
    }
}
