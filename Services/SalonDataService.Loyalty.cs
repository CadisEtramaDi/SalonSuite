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

    private void LoadLoyaltyRewardsFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, Title, PointsRequired, DiscountValue, Description, IsActive FROM dbo.LoyaltyRewards ORDER BY Id ASC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<LoyaltyRewardItem>();
            while (reader.Read())
            {
                list.Add(new LoyaltyRewardItem
                {
                    Id = reader.GetInt32(0),
                    Title = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    PointsRequired = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                    DiscountValue = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                    Description = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    IsActive = reader.IsDBNull(5) || reader.GetBoolean(5)
                });
            }
            if (list.Count > 0) LoyaltyRewards = list;
        }
        catch { }
    }

    public void AddLoyaltyReward(LoyaltyRewardItem reward)
    {
        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.LoyaltyRewards (Title, PointsRequired, DiscountValue, Description, IsActive) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@Title, @PointsRequired, @DiscountValue, @Description, @IsActive);",
            new SqlParameter("@Title", reward.Title),
            new SqlParameter("@PointsRequired", reward.PointsRequired),
            new SqlParameter("@DiscountValue", reward.DiscountValue),
            new SqlParameter("@Description", (object?)reward.Description ?? DBNull.Value),
            new SqlParameter("@IsActive", reward.IsActive)
        );

        reward.Id = dbId > 0 ? dbId : (LoyaltyRewards.Count > 0 ? LoyaltyRewards.Max(r => r.Id) + 1 : 1);
        LoyaltyRewards.Add(reward);
        NotifyStateChanged();
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

            ExecuteSqlNonQuery(
                "UPDATE dbo.LoyaltyRewards SET Title = @Title, PointsRequired = @PointsRequired, " +
                "DiscountValue = @DiscountValue, Description = @Description, IsActive = @IsActive WHERE Id = @Id;",
                new SqlParameter("@Id", existing.Id),
                new SqlParameter("@Title", existing.Title),
                new SqlParameter("@PointsRequired", existing.PointsRequired),
                new SqlParameter("@DiscountValue", existing.DiscountValue),
                new SqlParameter("@Description", (object?)existing.Description ?? DBNull.Value),
                new SqlParameter("@IsActive", existing.IsActive)
            );

            NotifyStateChanged();
        }
    }

    public void DeleteLoyaltyReward(int id)
    {
        LoyaltyRewards.RemoveAll(r => r.Id == id);
        ExecuteSqlNonQuery("DELETE FROM dbo.LoyaltyRewards WHERE Id = @Id;", new SqlParameter("@Id", id));
        NotifyStateChanged();
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
