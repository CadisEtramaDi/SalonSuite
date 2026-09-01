using System;
using System.Collections.Generic;
using System.Linq;
using SalonSuite.Models;

namespace SalonSuite.Services;

public class SalonDataService
{
    public event Action? OnChange;

    public List<ServiceOfferItem> Services { get; private set; } = new();
    public List<TeamMemberItem> Team { get; private set; } = new();
    public List<ServicePackageItem> Packages { get; private set; } = new();
    public List<TestimonialItem> Reviews { get; private set; } = new();
    public List<AppointmentRecord> Appointments { get; private set; } = new();
    public List<InvoiceRecord> Invoices { get; private set; } = new();
    public UserSession CurrentUser { get; private set; } = new();

    public SalonDataService()
    {
        SeedData();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    public void LoginAs(string role, string name, string email)
    {
        CurrentUser = new UserSession
        {
            IsLoggedIn = true,
            Role = role,
            Name = name,
            Email = email
        };
        NotifyStateChanged();
    }

    public void Logout()
    {
        CurrentUser = new UserSession();
        NotifyStateChanged();
    }

    public void AddAppointment(AppointmentRecord appointment)
    {
        appointment.Id = Appointments.Count > 0 ? Appointments.Max(a => a.Id) + 1 : 1;
        Appointments.Add(appointment);
        NotifyStateChanged();
    }

    public void UpdateAppointmentStatus(int id, string newStatus)
    {
        var appt = Appointments.FirstOrDefault(a => a.Id == id);
        if (appt != null)
        {
            appt.Status = newStatus;
            NotifyStateChanged();
        }
    }

    public void UpdateAppointmentNotes(int id, string notes)
    {
        var appt = Appointments.FirstOrDefault(a => a.Id == id);
        if (appt != null)
        {
            appt.Notes = notes;
            NotifyStateChanged();
        }
    }

    public void AddInvoice(InvoiceRecord invoice)
    {
        invoice.Id = Invoices.Count > 0 ? Invoices.Max(i => i.Id) + 1 : 1;
        Invoices.Add(invoice);

        var appt = Appointments.FirstOrDefault(a => a.Id == invoice.AppointmentId);
        if (appt != null)
        {
            appt.IsPaid = true;
            appt.Status = "Completed";
        }

        NotifyStateChanged();
    }

    public void AddTeamMember(TeamMemberItem member)
    {
        member.Id = Team.Count > 0 ? Team.Max(t => t.Id) + 1 : 1;
        Team.Add(member);
        NotifyStateChanged();
    }

    private void SeedData()
    {
        Services = new List<ServiceOfferItem>
        {
            new() { Number = "01", Name = "Haircut", Description = "Precision cuts tailored to your style", Price = 850 },
            new() { Number = "02", Name = "Color", Description = "Expert color treatments and balayage", Price = 1500 },
            new() { Number = "03", Name = "Styling", Description = "Professional styling for any occasion", Price = 650 },
            new() { Number = "04", Name = "Treatment", Description = "Deep conditioning and repair", Price = 950 }
        };

        Team = new List<TeamMemberItem>
        {
            new()
            {
                Id = 1,
                Name = "Sofia Martinez",
                Role = "Creative Director",
                ImageUrl = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=500&auto=format&fit=crop&q=80"
            },
            new()
            {
                Id = 2,
                Name = "James Anderson",
                Role = "Color Specialist",
                ImageUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=500&auto=format&fit=crop&q=80"
            },
            new()
            {
                Id = 3,
                Name = "Isabella Chen",
                Role = "Senior Stylist",
                ImageUrl = "https://images.unsplash.com/photo-1517841905240-472988babdf9?w=500&auto=format&fit=crop&q=80"
            }
        };

        Packages = new List<ServicePackageItem>
        {
            new()
            {
                Id = 1,
                Name = "Essential",
                Price = 1990,
                IsPopular = false,
                Features = new() { "Haircut", "Basic Treatment", "Hair Styling", "Consultation" }
            },
            new()
            {
                Id = 2,
                Name = "Signature",
                Price = 3990,
                IsPopular = true,
                Features = new() { "Haircut", "Color Service", "Deep Treatment", "Premium Styling", "Product Kit" }
            },
            new()
            {
                Id = 3,
                Name = "Ultimate",
                Price = 5990,
                IsPopular = false,
                Features = new() { "Haircut", "Full Color", "Hair Treatment", "Luxury Styling", "Products", "Priority Booking" }
            }
        };

        Reviews = new List<TestimonialItem>
        {
            new()
            {
                Id = 1,
                Quote = "The best salon experience. Sofia transformed my hair completely and I couldn't be happier with the results!",
                Author = "Alexandra Rivera",
                Rating = 5
            },
            new()
            {
                Id = 2,
                Quote = "Professional and worth every penny. The team really listens to what you want and delivers beyond expectations.",
                Author = "Marcus Thompson",
                Rating = 5
            },
            new()
            {
                Id = 3,
                Quote = "From the moment you walk in, you feel special. The color treatment I received was perfect.",
                Author = "Natalia Kim",
                Rating = 5
            },
            new()
            {
                Id = 4,
                Quote = "Every visit feels like a special occasion. The stylists are true artists and the results speak for themselves.",
                Author = "David Chen",
                Rating = 5
            }
        };

        var today = DateTime.Today;
        Appointments = new List<AppointmentRecord>
        {
            new()
            {
                Id = 1,
                ClientName = "Alexandra Rivera",
                ClientPhone = "+63 917 555 0192",
                ClientEmail = "alexandra.r@gmail.com",
                ServiceName = "Color - Signature Balayage",
                StylistName = "James Anderson",
                Date = today,
                TimeSlot = "10:00 AM",
                Price = 3990,
                Status = "In Progress"
            },
            new()
            {
                Id = 2,
                ClientName = "Marcus Thompson",
                ClientPhone = "+63 918 444 8812",
                ClientEmail = "m.thompson@corp.ph",
                ServiceName = "Haircut & Beard Sculpt",
                StylistName = "Sofia Martinez",
                Date = today,
                TimeSlot = "11:30 AM",
                Price = 850,
                Status = "Confirmed"
            },
            new()
            {
                Id = 3,
                ClientName = "Natalia Kim",
                ClientPhone = "+63 920 333 1290",
                ClientEmail = "natalia.kim@fashion.ph",
                ServiceName = "Signature Package",
                StylistName = "Sofia Martinez",
                Date = today,
                TimeSlot = "02:00 PM",
                Price = 3990,
                Status = "Confirmed"
            },
            new()
            {
                Id = 4,
                ClientName = "David Chen",
                ClientPhone = "+63 915 222 9011",
                ClientEmail = "david.chen@studio.com",
                ServiceName = "Hair Treatment & Styling",
                StylistName = "Isabella Chen",
                Date = today,
                TimeSlot = "04:30 PM",
                Price = 1600,
                Status = "Confirmed"
            }
        };

        Invoices = new List<InvoiceRecord>
        {
            new()
            {
                Id = 1,
                AppointmentId = 101,
                ClientName = "Eleanor Vance",
                ServiceName = "Signature Balayage & Gloss",
                StylistName = "James Anderson",
                Subtotal = 3990,
                Discount = 0,
                Total = 3990,
                AmountPaid = 4000,
                PaymentMethod = "Cash",
                Timestamp = DateTime.Today.AddHours(9).AddMinutes(45),
                CashierName = "Clara Santos"
            },
            new()
            {
                Id = 2,
                AppointmentId = 102,
                ClientName = "Roberto Mendoza",
                ServiceName = "Executive Haircut & Beard Sculpt",
                StylistName = "Sofia Martinez",
                Subtotal = 850,
                Discount = 170, // 20% Senior
                Total = 680,
                AmountPaid = 680,
                PaymentMethod = "GCash",
                Timestamp = DateTime.Today.AddHours(11).AddMinutes(15),
                CashierName = "Clara Santos"
            },
            new()
            {
                Id = 3,
                AppointmentId = 103,
                ClientName = "Chloe Dela Cruz",
                ServiceName = "Ultimate Luxury Hair Ritual",
                StylistName = "Isabella Chen",
                Subtotal = 5990,
                Discount = 0,
                Total = 5990,
                AmountPaid = 5990,
                PaymentMethod = "Credit Card",
                Timestamp = DateTime.Today.AddHours(12).AddMinutes(30),
                CashierName = "Clara Santos"
            }
        };
    }
}
