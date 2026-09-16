using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<LoyaltyRewardItem> LoyaltyRewards { get; private set; } = new();

    private async Task LoadLoyaltyRewardsFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("loyaltyRewards").GetSnapshotAsync();
            var list = new List<LoyaltyRewardItem>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<LoyaltyRewardItem>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                LoyaltyRewards = list.OrderBy(r => r.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading loyalty rewards: {ex.Message}");
        }
    }

    public void AddLoyaltyReward(LoyaltyRewardItem reward)
    {
        reward.Id = LoyaltyRewards.Count > 0 ? LoyaltyRewards.Max(r => r.Id) + 1 : 1;
        LoyaltyRewards.Add(reward);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("loyaltyRewards", reward.Id.ToString(), reward));
    }

    public void UpdateLoyaltyReward(LoyaltyRewardItem reward)
    {
        var existing = LoyaltyRewards.FirstOrDefault(r => r.Id == reward.Id);
        if (existing != null)
        {
            existing.Title = reward.Title;
            existing.PointsRequired = reward.PointsRequired;
            existing.DiscountValue = reward.DiscountValue;
            existing.Description = reward.Description;
            existing.IsActive = reward.IsActive;

            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("loyaltyRewards", existing.Id.ToString(), existing));
        }
    }

    public void DeleteLoyaltyReward(int id)
    {
        LoyaltyRewards.RemoveAll(r => r.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("loyaltyRewards", id.ToString()));
    }

    public static List<LoyaltyRewardItem> GetDefaultLoyaltyRewards() => new()
    {
        new()
        {
            Id = 1,
            Title = "Free Scalp Detox Treatment",
            PointsRequired = 200,
            DiscountValue = 350,
            Description = "Redeem 200 points for a complimentary scalp massage & tonic ritual.",
            IsActive = true
        },
        new()
        {
            Id = 2,
            Title = "₱500 Beauty Voucher",
            PointsRequired = 400,
            DiscountValue = 500,
            Description = "Redeem 400 points for ₱500 off any treatment or service package.",
            IsActive = true
        },
        new()
        {
            Id = 3,
            Title = "₱1,000 Luxury VIP Pass",
            PointsRequired = 750,
            DiscountValue = 1000,
            Description = "Redeem 750 points for ₱1,000 credit towards any luxury balayage or ritual.",
            IsActive = true
        }
    };
}
