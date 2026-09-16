using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<TestimonialItem> Reviews { get; private set; } = new();

    private void InitStaticContent()
    {
        if (Packages.Count == 0)
        {
            Packages = GetDefaultPackages();
        }

        if (Services.Count == 0)
        {
            Services = GetDefaultServices();
        }

        if (Team.Count == 0)
        {
            Team = GetDefaultTeam();
        }

        if (Suppliers.Count == 0)
        {
            Suppliers = GetDefaultSuppliers();
        }

        if (Products.Count == 0)
        {
            Products = GetDefaultProducts();
        }

        if (Promotions.Count == 0)
        {
            Promotions = GetDefaultPromotions();
        }

        if (LoyaltyRewards.Count == 0)
        {
            LoyaltyRewards = GetDefaultLoyaltyRewards();
        }

        if (Reviews.Count == 0)
        {
            Reviews = GetDefaultReviews();
        }
    }

    private async Task PersistInitialSeedToFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            foreach (var cust in Customers)
            {
                await SaveDocAsync("customers", cust.Id.ToString(), cust);
            }

            foreach (var emp in Team)
            {
                await SaveDocAsync("employees", emp.Id.ToString(), emp);
            }

            foreach (var sup in Suppliers)
            {
                await SaveDocAsync("suppliers", sup.Id.ToString(), sup);
            }

            foreach (var srv in Services)
            {
                await SaveDocAsync("services", srv.Id.ToString(), srv);
            }

            foreach (var prd in Products)
            {
                await SaveDocAsync("products", prd.Id.ToString(), prd);
            }

            foreach (var prm in Promotions)
            {
                await SaveDocAsync("promotions", prm.Id.ToString(), prm);
            }

            foreach (var rwd in LoyaltyRewards)
            {
                await SaveDocAsync("loyaltyRewards", rwd.Id.ToString(), rwd);
            }

            foreach (var appt in Appointments)
            {
                await SaveDocAsync("appointments", appt.Id.ToString(), appt);
            }

            foreach (var inv in Invoices)
            {
                await SaveDocAsync("invoices", inv.Id.ToString(), inv);
            }

            Console.WriteLine("[Firebase] Successfully initialized and seeded Firestore collections.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Notice seeding Firestore: {ex.Message}");
        }
    }

    private void SeedData()
    {
        // 1. Initial CRM Customers (Maps to dbo.Customers in SSMS)
        Customers = new List<CustomerRecord>
        {
            new()
            {
                Id = 1,
                FullName = "Alexandra Rivera",
                Email = "alexandra.r@gmail.com",
                Phone = "+63 917 555 0192",
                LoyaltyPoints = 450,
                Tier = "Gold",
                TotalSpent = 14500,
                VisitsCount = 6,
                LastVisit = DateTime.SpecifyKind(DateTime.Today.AddDays(-2), DateTimeKind.Utc),
                Notes = "Prefers cool ash blonde balayage, sensitive scalp.",
                CreatedAt = DateTime.SpecifyKind(DateTime.Today.AddMonths(-4), DateTimeKind.Utc)
            },
            new()
            {
                Id = 2,
                FullName = "Marcus Thompson",
                Email = "m.thompson@corp.ph",
                Phone = "+63 918 444 8812",
                LoyaltyPoints = 120,
                Tier = "Silver",
                TotalSpent = 4200,
                VisitsCount = 3,
                LastVisit = DateTime.SpecifyKind(DateTime.Today.AddDays(-5), DateTimeKind.Utc),
                Notes = "Executive cut with beard line sculpting.",
                CreatedAt = DateTime.SpecifyKind(DateTime.Today.AddMonths(-3), DateTimeKind.Utc)
            },
            new()
            {
                Id = 3,
                FullName = "Natalia Kim",
                Email = "natalia.kim@fashion.ph",
                Phone = "+63 920 333 1290",
                LoyaltyPoints = 850,
                Tier = "Platinum",
                TotalSpent = 28900,
                VisitsCount = 9,
                LastVisit = DateTime.SpecifyKind(DateTime.Today.AddDays(-1), DateTimeKind.Utc),
                Notes = "VIP client. Always books Signature package with Sofia.",
                CreatedAt = DateTime.SpecifyKind(DateTime.Today.AddMonths(-6), DateTimeKind.Utc)
            },
            new()
            {
                Id = 4,
                FullName = "David Chen",
                Email = "david.chen@studio.com",
                Phone = "+63 915 222 9011",
                LoyaltyPoints = 80,
                Tier = "Bronze",
                TotalSpent = 2450,
                VisitsCount = 2,
                LastVisit = DateTime.SpecifyKind(DateTime.Today.AddDays(-10), DateTimeKind.Utc),
                Notes = "Weekend styling and scalp massage ritual.",
                CreatedAt = DateTime.SpecifyKind(DateTime.Today.AddMonths(-1), DateTimeKind.Utc)
            }
        };

        // 2. Services (Base Salon Catalog)
        Services = GetDefaultServices();

        // 3. Service Packages
        Packages = GetDefaultPackages();

        // 4. Team Members (Employees)
        Team = GetDefaultTeam();

        // 5. Suppliers (Maps to dbo.Suppliers in SSMS)
        Suppliers = GetDefaultSuppliers();

        // 6. Products (Maps to dbo.Products in SSMS)
        Products = GetDefaultProducts();

        // 7. Promotions (Maps to dbo.Promotions in SSMS)
        Promotions = GetDefaultPromotions();

        // 8. Loyalty Rewards (Maps to dbo.LoyaltyRewards in SSMS)
        LoyaltyRewards = GetDefaultLoyaltyRewards();

        // 9. Appointments (Maps to dbo.Appointments in SSMS)
        Appointments = new List<AppointmentRecord>
        {
            new()
            {
                Id = 1,
                CustomerId = 1,
                ClientName = "Alexandra Rivera",
                ClientPhone = "+63 917 555 0192",
                ClientEmail = "alexandra.r@gmail.com",
                ServiceName = "Signature Package",
                StylistName = "Sofia Martinez",
                Date = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                TimeSlot = "10:30 AM",
                Price = 3990,
                Status = "Completed",
                IsPaid = true,
                Notes = "Dimension Balayage & Keratin Bond Infusion"
            },
            new()
            {
                Id = 2,
                CustomerId = 3,
                ClientName = "Natalia Kim",
                ClientPhone = "+63 920 333 1290",
                ClientEmail = "natalia.kim@fashion.ph",
                ServiceName = "Ultimate Package",
                StylistName = "James Anderson",
                Date = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                TimeSlot = "02:30 PM",
                Price = 5990,
                Status = "In Progress",
                IsPaid = false,
                Notes = "Full Head Spa + Runway Thermal Styling"
            },
            new()
            {
                Id = 3,
                CustomerId = 2,
                ClientName = "Marcus Thompson",
                ClientPhone = "+63 918 444 8812",
                ClientEmail = "m.thompson@corp.ph",
                ServiceName = "01 Precision Haircut",
                StylistName = "Isabella Chen",
                Date = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                TimeSlot = "04:00 PM",
                Price = 850,
                Status = "Confirmed",
                IsPaid = false,
                Notes = "Executive precision cut and scalp cleanse"
            }
        };

        // 10. Invoices (Maps to dbo.Invoices in SSMS)
        Invoices = new List<InvoiceRecord>
        {
            new()
            {
                Id = 1,
                AppointmentId = 1,
                CustomerId = 1,
                ClientName = "Alexandra Rivera",
                ServiceName = "Signature Package",
                StylistName = "Sofia Martinez",
                Subtotal = 3990,
                RetailAddonsTotal = 0,
                Discount = 0,
                PromoCode = null,
                PromoDiscount = 0,
                LoyaltyPointsRedeemed = 0,
                LoyaltyDiscount = 0,
                Total = 3990,
                AmountPaid = 4000,
                PaymentMethod = "GCash",
                Timestamp = DateTime.SpecifyKind(DateTime.Today.AddHours(11).AddMinutes(45), DateTimeKind.Utc),
                CashierName = "Clara Santos"
            }
        };

        InitStaticContent();
        SyncAllDataRelationships();
    }

    public void SyncAllDataRelationships()
    {
        // 1. Ensure all appointments and invoices have corresponding CRM customer profiles
        foreach (var appt in Appointments)
        {
            if (!string.IsNullOrWhiteSpace(appt.ClientName))
            {
                var cust = EnsureCustomer(appt.ClientName, appt.ClientPhone, appt.ClientEmail, appt.Notes);
                appt.CustomerId = cust.Id;
            }
        }

        foreach (var inv in Invoices)
        {
            if (!string.IsNullOrWhiteSpace(inv.ClientName))
            {
                var cust = EnsureCustomer(inv.ClientName);
                inv.CustomerId = cust.Id;
            }
        }

        // 2. Recompute Customer lifetime statistics
        foreach (var customer in Customers)
        {
            var customerInvoices = Invoices.Where(i => (i.CustomerId.HasValue && i.CustomerId.Value == customer.Id) ||
                                                       (!string.IsNullOrEmpty(i.ClientName) && i.ClientName.Equals(customer.FullName, StringComparison.OrdinalIgnoreCase))).ToList();
            if (customerInvoices.Any())
            {
                var invoicedTotal = customerInvoices.Sum(i => i.Total);
                if (invoicedTotal > customer.TotalSpent)
                {
                    customer.TotalSpent = invoicedTotal;
                }
                customer.VisitsCount = Math.Max(customer.VisitsCount, customerInvoices.Count);
                customer.LastVisit = customerInvoices.Max(i => i.Timestamp);
                customer.Tier = CalculateTier(customer.TotalSpent);
            }
        }
    }

    public static List<TestimonialItem> GetDefaultReviews() => new()
    {
        new() { Id = 1, Author = "Alexandra Rivera", Quote = "Sofia transformed my color completely. The dimension is seamless and my hair feels healthier than ever!", Rating = 5 },
        new() { Id = 2, Author = "Natalia Kim", Quote = "The Ultimate Package head spa is pure bliss. Truly a 5-star sanctuary experience in Taguig.", Rating = 5 },
        new() { Id = 3, Author = "Marcus Thompson", Quote = "Precision haircut was fast, meticulous, and styled to perfection. Highly recommended!", Rating = 5 }
    };
}
