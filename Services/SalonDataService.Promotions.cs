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

    private void LoadPromotionsFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, Code, Title, DiscountType, DiscountValue, MinSpend, StartDate, EndDate, IsActive, UsageCount FROM dbo.Promotions ORDER BY Id ASC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<PromotionItem>();
            while (reader.Read())
            {
                list.Add(new PromotionItem
                {
                    Id = reader.GetInt32(0),
                    Code = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Title = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    DiscountType = reader.IsDBNull(3) ? "Percentage" : reader.GetString(3),
                    DiscountValue = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                    MinSpend = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                    StartDate = reader.IsDBNull(6) ? DateTime.Today : reader.GetDateTime(6),
                    EndDate = reader.IsDBNull(7) ? DateTime.Today.AddMonths(1) : reader.GetDateTime(7),
                    IsActive = reader.IsDBNull(8) || reader.GetBoolean(8),
                    UsageCount = reader.IsDBNull(9) ? 0 : reader.GetInt32(9)
                });
            }
            if (list.Count > 0) Promotions = list;
        }
        catch { }
    }

    public void AddPromotion(PromotionItem promo)
    {
        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.Promotions (Code, Title, DiscountType, DiscountValue, MinSpend, StartDate, EndDate, IsActive, UsageCount) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@Code, @Title, @DiscountType, @DiscountValue, @MinSpend, @StartDate, @EndDate, @IsActive, @UsageCount);",
            new SqlParameter("@Code", promo.Code),
            new SqlParameter("@Title", promo.Title),
            new SqlParameter("@DiscountType", promo.DiscountType),
            new SqlParameter("@DiscountValue", promo.DiscountValue),
            new SqlParameter("@MinSpend", promo.MinSpend),
            new SqlParameter("@StartDate", promo.StartDate.Date),
            new SqlParameter("@EndDate", promo.EndDate.Date),
            new SqlParameter("@IsActive", promo.IsActive),
            new SqlParameter("@UsageCount", promo.UsageCount)
        );

        promo.Id = dbId > 0 ? dbId : (Promotions.Count > 0 ? Promotions.Max(p => p.Id) + 1 : 1);
        Promotions.Add(promo);
        NotifyStateChanged();
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
            existing.StartDate = promo.StartDate;
            existing.EndDate = promo.EndDate;
            existing.IsActive = promo.IsActive;

            ExecuteSqlNonQuery(
                "UPDATE dbo.Promotions SET Code = @Code, Title = @Title, DiscountType = @DiscountType, " +
                "DiscountValue = @DiscountValue, MinSpend = @MinSpend, StartDate = @StartDate, EndDate = @EndDate, " +
                "IsActive = @IsActive WHERE Id = @Id;",
                new SqlParameter("@Id", existing.Id),
                new SqlParameter("@Code", existing.Code),
                new SqlParameter("@Title", existing.Title),
                new SqlParameter("@DiscountType", existing.DiscountType),
                new SqlParameter("@DiscountValue", existing.DiscountValue),
                new SqlParameter("@MinSpend", existing.MinSpend),
                new SqlParameter("@StartDate", existing.StartDate.Date),
                new SqlParameter("@EndDate", existing.EndDate.Date),
                new SqlParameter("@IsActive", existing.IsActive)
            );

            NotifyStateChanged();
        }
    }

    public void DeletePromotion(int id)
    {
        Promotions.RemoveAll(p => p.Id == id);
        ExecuteSqlNonQuery("DELETE FROM dbo.Promotions WHERE Id = @Id;", new SqlParameter("@Id", id));
        NotifyStateChanged();
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
            StartDate = DateTime.Today.AddDays(-30),
            EndDate = DateTime.Today.AddMonths(3),
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
            StartDate = DateTime.Today.AddDays(-15),
            EndDate = DateTime.Today.AddMonths(2),
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
            StartDate = DateTime.Today.AddDays(-7),
            EndDate = DateTime.Today.AddMonths(1),
            IsActive = true,
            UsageCount = 9
        }
    };
}
