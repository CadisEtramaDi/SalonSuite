using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<ServiceOfferItem> Services { get; private set; } = new();
    public List<ServicePackageItem> Packages { get; private set; } = new();

    private async Task LoadServicesFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("services").GetSnapshotAsync();
            var list = new List<ServiceOfferItem>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<ServiceOfferItem>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    if (string.IsNullOrEmpty(item.ImageUrl))
                    {
                        item.ImageUrl = GetServiceImageUrl(item.Name, item.Category);
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Services = list.OrderBy(s => s.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading services: {ex.Message}");
        }
    }

    public void AddService(ServiceOfferItem service)
    {
        if (string.IsNullOrWhiteSpace(service.Number))
        {
            service.Number = $"{Services.Count + 1:D2}";
        }
        if (string.IsNullOrEmpty(service.ImageUrl))
        {
            service.ImageUrl = GetServiceImageUrl(service.Name, service.Category);
        }

        service.Id = Services.Count > 0 ? Services.Max(s => s.Id) + 1 : 1;
        Services.Add(service);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("services", service.Id.ToString(), service));
    }

    public void UpdateService(ServiceOfferItem service)
    {
        var existing = Services.FirstOrDefault(s => s.Id == service.Id);
        if (existing != null)
        {
            existing.Name = service.Name;
            existing.Number = service.Number;
            existing.Category = service.Category;
            existing.Description = service.Description;
            existing.Price = service.Price;
            existing.DurationMinutes = service.DurationMinutes;
            existing.ImageUrl = service.ImageUrl;
            existing.IsActive = service.IsActive;

            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("services", existing.Id.ToString(), existing));
        }
    }

    public void DeleteService(int id)
    {
        Services.RemoveAll(s => s.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("services", id.ToString()));
    }

    public void AddPackage(ServicePackageItem package)
    {
        package.Id = Packages.Count > 0 ? Packages.Max(p => p.Id) + 1 : 1;
        Packages.Add(package);
        NotifyStateChanged();
    }

    public void UpdatePackage(ServicePackageItem package)
    {
        var existing = Packages.FirstOrDefault(p => p.Id == package.Id);
        if (existing != null)
        {
            existing.Name = package.Name;
            existing.Description = package.Description;
            existing.ImageUrl = package.ImageUrl;
            existing.Price = package.Price;
            existing.DurationMinutes = package.DurationMinutes;
            existing.IsPopular = package.IsPopular;
            existing.Features = package.Features;
            NotifyStateChanged();
        }
    }

    public void DeletePackage(int id)
    {
        Packages.RemoveAll(p => p.Id == id);
        NotifyStateChanged();
    }

    public static string GetServiceImageUrl(string name, string category)
    {
        if (name.Contains("Updo", StringComparison.OrdinalIgnoreCase) || name.Contains("Bridal", StringComparison.OrdinalIgnoreCase))
            return "https://images.unsplash.com/photo-1519741497674-611481863552?w=800&auto=format&fit=crop&q=80";
        if (name.Contains("Waves", StringComparison.OrdinalIgnoreCase) || name.Contains("Curl", StringComparison.OrdinalIgnoreCase))
            return "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?w=800&auto=format&fit=crop&q=80";
        if (name.Contains("Blowout", StringComparison.OrdinalIgnoreCase) || name.Contains("Styling", StringComparison.OrdinalIgnoreCase))
            return "https://images.unsplash.com/photo-1521590832167-7bcbfaa6381f?w=800&auto=format&fit=crop&q=80";
        if (name.Contains("Fade", StringComparison.OrdinalIgnoreCase) || name.Contains("Men", StringComparison.OrdinalIgnoreCase) || name.Contains("Beard", StringComparison.OrdinalIgnoreCase))
            return "https://images.unsplash.com/photo-1503951914875-452162b0f3f1?w=800&auto=format&fit=crop&q=80";
        if (name.Contains("Balayage", StringComparison.OrdinalIgnoreCase) || name.Contains("Highlight", StringComparison.OrdinalIgnoreCase) || category.Equals("Color", StringComparison.OrdinalIgnoreCase))
            return "https://images.unsplash.com/photo-1562322140-8baeececf3df?w=800&auto=format&fit=crop&q=80";
        if (name.Contains("Keratin", StringComparison.OrdinalIgnoreCase) || category.Equals("Treatment", StringComparison.OrdinalIgnoreCase))
            return "https://images.unsplash.com/photo-1519699047748-de8e457a634e?w=800&auto=format&fit=crop&q=80";
        if (category.Equals("Spa", StringComparison.OrdinalIgnoreCase) || name.Contains("Spa", StringComparison.OrdinalIgnoreCase) || name.Contains("Scalp", StringComparison.OrdinalIgnoreCase))
            return "https://images.unsplash.com/photo-1580618672591-eb180b1a973f?w=800&auto=format&fit=crop&q=80";
        return "https://images.unsplash.com/photo-1560066984-138dadb4c035?w=800&auto=format&fit=crop&q=80";
    }

    public static List<ServiceOfferItem> GetDefaultServices() => new()
    {
        new()
        {
            Id = 1,
            Number = "01",
            Name = "Bespoke Precision Haircut",
            Category = "Hair",
            Description = "Customized consultation and master haircut sculpted to complement your facial morphology. Includes botanical shampoo, scalp massage, and blowout.",
            Price = 850,
            DurationMinutes = 45,
            ImageUrl = "https://images.unsplash.com/photo-1560066984-138dadb4c035?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 2,
            Number = "02",
            Name = "Red Carpet Blowout & Styling",
            Category = "Styling",
            Description = "Voluminous runway blowout with thermal shine finish, bouncy curls, or silky glass hair sculpting.",
            Price = 650,
            DurationMinutes = 35,
            ImageUrl = "https://images.unsplash.com/photo-1521590832167-7bcbfaa6381f?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 3,
            Number = "03",
            Name = "Bridal & Gala Updo Hairstyle",
            Category = "Styling",
            Description = "Intricate formal wedding updo, romantic braided chignon, or red-carpet event styling with long-lasting hold.",
            Price = 1800,
            DurationMinutes = 60,
            ImageUrl = "https://images.unsplash.com/photo-1519741497674-611481863552?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 4,
            Number = "04",
            Name = "Beach Waves & Hollywood Curls Hairstyle",
            Category = "Styling",
            Description = "Effortless mermaid texture, undone beach waves, or cascading Hollywood glamour curls with thermal protection.",
            Price = 750,
            DurationMinutes = 40,
            ImageUrl = "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 5,
            Number = "05",
            Name = "French Dimensional Balayage & Glaze",
            Category = "Color",
            Description = "Hand-painted dimensional balayage with gloss glaze for seamless sun-kissed radiance, soft regrowth, and mirror shine.",
            Price = 1500,
            DurationMinutes = 90,
            ImageUrl = "https://images.unsplash.com/photo-1562322140-8baeececf3df?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 6,
            Number = "06",
            Name = "Full Foil Highlights & Tone",
            Category = "Color",
            Description = "Multidimensional precision foil highlights with seamless root shadow and customized gloss toning.",
            Price = 1400,
            DurationMinutes = 75,
            ImageUrl = "https://images.unsplash.com/photo-1580618672591-eb180b1a973f?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 7,
            Number = "07",
            Name = "Luminous Gloss & Neutralizing Toner",
            Category = "Color",
            Description = "Translucent demi-permanent color glaze that cancels unwanted brassiness, seals cuticle porosity, and infuses multi-angle reflection.",
            Price = 750,
            DurationMinutes = 30,
            ImageUrl = "https://images.unsplash.com/photo-1605497788044-5a32c7078486?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 8,
            Number = "08",
            Name = "Root Regrowth & Gray Coverage",
            Category = "Color",
            Description = "100% seamless root regrowth coverage using rich, conditioning European organic pigments.",
            Price = 1100,
            DurationMinutes = 60,
            ImageUrl = "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 9,
            Number = "09",
            Name = "Keratin Silk Protein Smoothing Ritual",
            Category = "Treatment",
            Description = "Intensive protein bond restructuring therapy that eliminates frizz, blocks humidity, and restores silky elasticity for 12 weeks.",
            Price = 950,
            DurationMinutes = 60,
            ImageUrl = "https://images.unsplash.com/photo-1519699047748-de8e457a634e?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 10,
            Number = "10",
            Name = "Deep Collagen & Moisture Mask Therapy",
            Category = "Treatment",
            Description = "Hydrolyzed marine collagen and argan lipid mask with micro-mist steam infusion for bleached, brittle, or dry hair.",
            Price = 700,
            DurationMinutes = 40,
            ImageUrl = "https://images.unsplash.com/photo-1527799820374-dcf8d9d4a388?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 11,
            Number = "11",
            Name = "Botanical Scalp Detox & Head Spa",
            Category = "Spa",
            Description = "Clarifying sea salt scrub, organic tonic infusion, ozone mist hydration, and rejuvenating Japanese shiatsu pressure massage.",
            Price = 1200,
            DurationMinutes = 50,
            ImageUrl = "https://images.unsplash.com/photo-1580618672591-eb180b1a973f?w=800&auto=format&fit=crop&q=80"
        },
        new()
        {
            Id = 12,
            Number = "12",
            Name = "Gentlemen's Precision Fade & Beard Sculpting",
            Category = "Hair",
            Description = "Clean scissor & clipper fade haircut, invigorating hot towel treatment, neck razor line, and conditioning beard balm.",
            Price = 600,
            DurationMinutes = 35,
            ImageUrl = "https://images.unsplash.com/photo-1503951914875-452162b0f3f1?w=800&auto=format&fit=crop&q=80"
        }
    };

    public static List<ServicePackageItem> GetDefaultPackages() => new()
    {
        new()
        {
            Id = 1,
            Name = "Essential",
            Description = "The ideal maintenance ritual for busy professionals seeking refined grooming, scalp refreshment, and high-gloss styling in one seamless visit.",
            ImageUrl = "https://images.unsplash.com/photo-1562322140-8baeececf3df?w=800&auto=format&fit=crop&q=80",
            Price = 1990,
            DurationMinutes = 75,
            IsPopular = false,
            Features = new() { "Bespoke Precision Haircut", "Clarifying Botanical Scalp Wash", "Couture Blowout & Thermal Finish", "Professional Stylist Consultation" }
        },
        new()
        {
            Id = 2,
            Name = "Signature",
            Description = "Our iconic total hair transformation experience. Combines custom dimension coloring, intensive structural repair, precision styling, and take-home elixir.",
            ImageUrl = "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?w=800&auto=format&fit=crop&q=80",
            Price = 3990,
            DurationMinutes = 150,
            IsPopular = true,
            Features = new() { "Master Precision Haircut", "Full Dimensional Color or Balayage", "Keratin Deep Bond Repair Ritual", "Red Carpet Signature Blowout", "Complimentary Take-Home Serum Kit" }
        },
        new()
        {
            Id = 3,
            Name = "Ultimate",
            Description = "The pinnacle of bespoke luxury care. A multi-hour restorative sanctuary featuring master artistry, holistic head spa, luxury balayage, and VIP treatment.",
            ImageUrl = "https://images.unsplash.com/photo-1633681926022-84c23e8cb2d6?w=800&auto=format&fit=crop&q=80",
            Price = 5990,
            DurationMinutes = 210,
            IsPopular = false,
            Features = new() { "Bespoke Master Hair Architecture", "Signature Balayage & Gloss Glaze", "Holistic Scalp Head Spa & Shiatsu", "VIP Runway Thermal Styling", "Full-size Luxury Care Gift Set", "Priority VIP Rescheduling Access" }
        }
    };
}
