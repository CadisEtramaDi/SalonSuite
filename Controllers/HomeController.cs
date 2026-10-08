using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using SalonSuite.Models;
using SalonSuite.Services;

namespace SalonSuite.Components.Pages;

public partial class Home : ComponentBase, IDisposable
{
    [Inject] public SalonDataService SalonService { get; set; } = default!;
    [Inject] public NavigationManager Navigation { get; set; } = default!;
    [Inject] public EmailReceiptService EmailService { get; set; } = default!;

    public class HeroSlideItem
    {
        public string ImageUrl { get; set; } = "";
        public string Title { get; set; } = "";
        public string Tagline { get; set; } = "";
    }

    private readonly List<HeroSlideItem> heroSlides = new()
    {
        new()
        {
            ImageUrl = "https://images.unsplash.com/photo-1522337360788-8b13dee7a37e?w=1200&auto=format&fit=crop&q=85",
            Title = "Couture Styling & Blowout",
            Tagline = "Red carpet signature volume sculpting & featherlight smoothing"
        },
        new()
        {
            ImageUrl = "https://images.unsplash.com/photo-1560869713-7d0a29430803?w=1200&auto=format&fit=crop&q=85",
            Title = "Signature Balayage & Toning",
            Tagline = "Multi-dimensional blonde architecture & seamless sun-kissed blending"
        },
        new()
        {
            ImageUrl = "https://images.unsplash.com/photo-1519699047748-de8e457a634e?w=1200&auto=format&fit=crop&q=85",
            Title = "Keratin Deep Bond Repair",
            Tagline = "Intensive protein bond infusion for lasting frizz-free silkiness"
        },
        new()
        {
            ImageUrl = "https://images.unsplash.com/photo-1580618672591-eb180b1a973f?w=1200&auto=format&fit=crop&q=85",
            Title = "Japanese Scalp Head Spa",
            Tagline = "Botanical clarifying scrub, micro-mist ozone & shiatsu pressure therapy"
        },
        new()
        {
            ImageUrl = "https://images.unsplash.com/photo-1605497788044-5a32c7078486?w=1200&auto=format&fit=crop&q=85",
            Title = "Luminous Gloss & Color Glaze",
            Tagline = "Translucent demi-permanent glaze for radiant multi-angle reflection"
        }
    };

    private int currentSlideIndex = 0;
    private System.Threading.Timer? autoSlideTimer;
    private bool isHovered = false;
    private bool isMobileNavOpen = false;

    private bool showBookingModal = false;
    private bool bookingConfirmed = false;
    private AppointmentRecord newAppt = new();

    protected override void OnInitialized()
    {
        SalonService.OnChange += HandleDataChanged;
        StartAutoSlideTimer();
    }

    private void HandleDataChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private void HandleLogout()
    {
        SalonService.Logout();
        StateHasChanged();
    }

    private void StartAutoSlideTimer()
    {
        autoSlideTimer?.Dispose();
        autoSlideTimer = new System.Threading.Timer(_ =>
        {
            if (!isHovered)
            {
                InvokeAsync(() =>
                {
                    NextSlide();
                    StateHasChanged();
                });
            }
        }, null, 4500, 4500);
    }

    private void NextSlide()
    {
        currentSlideIndex = (currentSlideIndex + 1) % heroSlides.Count;
    }

    private void PrevSlide()
    {
        currentSlideIndex = (currentSlideIndex - 1 + heroSlides.Count) % heroSlides.Count;
    }

    private void GoToSlide(int index)
    {
        if (index >= 0 && index < heroSlides.Count)
        {
            currentSlideIndex = index;
        }
    }

    public void Dispose()
    {
        SalonService.OnChange -= HandleDataChanged;
        autoSlideTimer?.Dispose();
    }

    private void OpenBookingModal(string initialServiceName)
    {
        bookingConfirmed = false;
        var resolvedService = ResolveServiceSelection(initialServiceName);
        newAppt = new AppointmentRecord
        {
            ServiceName = resolvedService,
            StylistName = "",
            Date = DateTime.Today.AddDays(1),
            TimeSlot = "10:30 AM",
            Price = GetPriceForSelectedService(resolvedService),
            Status = "Confirmed"
        };
        showBookingModal = true;
    }

    private string ResolveServiceSelection(string? inputName)
    {
        if (string.IsNullOrWhiteSpace(inputName))
        {
            return "Signature Package";
        }

        var matchedPkg = SalonService.Packages.FirstOrDefault(p =>
            p.Name.Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            $"{p.Name} Package".Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            inputName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedPkg != null)
        {
            return $"{matchedPkg.Name} Package";
        }

        var matchedSvc = SalonService.Services.FirstOrDefault(s =>
            s.Name.Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            $"{s.Number} {s.Name}".Equals(inputName, StringComparison.OrdinalIgnoreCase) ||
            inputName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedSvc != null)
        {
            return $"{matchedSvc.Number} {matchedSvc.Name}";
        }

        return "Signature Package";
    }

    private decimal GetPriceForSelectedService(string serviceName)
    {
        var matchedPkg = SalonService.Packages.FirstOrDefault(p =>
            serviceName.Contains(p.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedPkg != null) return matchedPkg.Price;

        var matchedSvc = SalonService.Services.FirstOrDefault(s =>
            serviceName.Contains(s.Name, StringComparison.OrdinalIgnoreCase));
        if (matchedSvc != null) return matchedSvc.Price;

        return 1990;
    }

    private void OnServiceSelectionChanged()
    {
        newAppt.Price = GetPriceForSelectedService(newAppt.ServiceName);
    }

    private void ConfirmBooking()
    {
        if (string.IsNullOrWhiteSpace(newAppt.ClientName)) return;

        if (string.IsNullOrWhiteSpace(newAppt.StylistName) ||
            newAppt.StylistName.Contains("Any", StringComparison.OrdinalIgnoreCase) ||
            newAppt.StylistName.Contains("No Preference", StringComparison.OrdinalIgnoreCase))
        {
            newAppt.StylistName = SalonService.FindAvailableStylist(newAppt.Date, newAppt.TimeSlot);
        }

        SalonService.AddAppointment(newAppt);
        bookingConfirmed = true;

        if (!string.IsNullOrWhiteSpace(newAppt.ClientEmail))
        {
            _ = EmailService.SendBookingConfirmationAsync(newAppt, newAppt.ClientEmail);
        }

        Task.Delay(1400).ContinueWith(_ =>
        {
            InvokeAsync(() =>
            {
                showBookingModal = false;
                StateHasChanged();
            });
        });
    }
}
