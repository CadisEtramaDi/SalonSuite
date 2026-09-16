using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<PromotionItem> Promotions { get; private set; } = new();

    private async Task LoadPromotionsFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("promotions").GetSnapshotAsync();
            var list = new List<PromotionItem>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<PromotionItem>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Promotions = list.OrderBy(p => p.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading promotions: {ex.Message}");
        }
    }

    public void AddPromotion(PromotionItem promo)
    {
        promo.Id = Promotions.Count > 0 ? Promotions.Max(p => p.Id) + 1 : 1;
        if (promo.StartDate.Kind != DateTimeKind.Utc)
        {
            promo.StartDate = DateTime.SpecifyKind(promo.StartDate, DateTimeKind.Utc);
        }
        if (promo.EndDate.Kind != DateTimeKind.Utc)
        {
            promo.EndDate = DateTime.SpecifyKind(promo.EndDate, DateTimeKind.Utc);
        }
        Promotions.Add(promo);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("promotions", promo.Id.ToString(), promo));
    }

    public void UpdatePromotion(PromotionItem promo)
    {
        var existing = Promotions.FirstOrDefault(p => p.Id == promo.Id);
        if (existing != null)
        {
            existing.Code = promo.Code;
            existing.Title = promo.Title;
            existing.DiscountType = promo.DiscountType;
            existing.DiscountValue = promo.DiscountValue;
            existing.MinSpend = promo.MinSpend;
            existing.StartDate = promo.StartDate.Kind != DateTimeKind.Utc
                ? DateTime.SpecifyKind(promo.StartDate, DateTimeKind.Utc)
                : promo.StartDate;
            existing.EndDate = promo.EndDate.Kind != DateTimeKind.Utc
                ? DateTime.SpecifyKind(promo.EndDate, DateTimeKind.Utc)
                : promo.EndDate;
            existing.IsActive = promo.IsActive;

            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("promotions", existing.Id.ToString(), existing));
        }
    }

    public void DeletePromotion(int id)
    {
        Promotions.RemoveAll(p => p.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("promotions", id.ToString()));
    }

    public (bool IsValid, decimal DiscountAmount, string Message) ValidatePromo(string code, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, 0, "No promo code entered.");
        }

        var promo = Promotions.FirstOrDefault(p => p.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
        if (promo == null)
        {
            return (false, 0, $"Promo code '{code}' is invalid.");
        }

        if (!promo.IsActive || DateTime.Today < promo.StartDate || DateTime.Today > promo.EndDate)
        {
            return (false, 0, $"Promo code '{code}' has expired or is inactive.");
        }

        if (subtotal < promo.MinSpend)
        {
            return (false, 0, $"Minimum spend of ₱{promo.MinSpend:N0} required for this promo.");
        }

        decimal discount = promo.DiscountType == "Percentage"
            ? Math.Round(subtotal * (promo.DiscountValue / 100m), 2)
            : promo.DiscountValue;

        return (true, discount, $"Promo '{promo.Code}' applied successfully (-₱{discount:N0})!");
    }

    public static List<PromotionItem> GetDefaultPromotions() => new()
    {
        new()
        {
            Id = 1,
            Code = "WELCOME10",
            Title = "New Client First Visit 10% OFF",
            DiscountType = "Percentage",
            DiscountValue = 10,
            MinSpend = 500,
            StartDate = DateTime.SpecifyKind(DateTime.Today.AddDays(-30), DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(DateTime.Today.AddMonths(3), DateTimeKind.Utc),
            IsActive = true,
            UsageCount = 14
        },
        new()
        {
            Id = 2,
            Code = "VIP200",
            Title = "VIP Flat Voucher ₱200 OFF",
            DiscountType = "Fixed",
            DiscountValue = 200,
            MinSpend = 1500,
            StartDate = DateTime.SpecifyKind(DateTime.Today.AddDays(-15), DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(DateTime.Today.AddMonths(2), DateTimeKind.Utc),
            IsActive = true,
            UsageCount = 28
        },
        new()
        {
            Id = 3,
            Code = "SUMMERGLOW",
            Title = "Summer Balayage Special 15% OFF",
            DiscountType = "Percentage",
            DiscountValue = 15,
            MinSpend = 2500,
            StartDate = DateTime.SpecifyKind(DateTime.Today.AddDays(-7), DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(DateTime.Today.AddMonths(1), DateTimeKind.Utc),
            IsActive = true,
            UsageCount = 9
        }
    };
}
