using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<ProductItem> Products { get; private set; } = new();

    private void LoadProductsFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, SKU, Name, Category, CostPrice, RetailPrice, StockQuantity, ReorderLevel, SupplierId, SupplierName, Unit FROM dbo.Products ORDER BY Id DESC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<ProductItem>();
            while (reader.Read())
            {
                list.Add(new ProductItem
                {
                    Id = reader.GetInt32(0),
                    SKU = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Name = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Category = reader.IsDBNull(3) ? "Retail" : reader.GetString(3),
                    CostPrice = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                    RetailPrice = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                    StockQuantity = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                    ReorderLevel = reader.IsDBNull(7) ? 5 : reader.GetInt32(7),
                    SupplierId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                    SupplierName = reader.IsDBNull(9) ? "" : reader.GetString(9),
                    Unit = reader.IsDBNull(10) ? "Bottle" : reader.GetString(10)
                });
            }
            if (list.Count > 0) Products = list;
        }
        catch { }
    }

    public void AddProduct(ProductItem product)
    {
        if (string.IsNullOrWhiteSpace(product.SKU))
        {
            product.SKU = $"SKU-PRD-{Products.Count + 1:D3}";
        }

        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.Products (SKU, Name, Category, CostPrice, RetailPrice, StockQuantity, ReorderLevel, SupplierId, SupplierName, Unit) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@SKU, @Name, @Category, @CostPrice, @RetailPrice, @StockQuantity, @ReorderLevel, @SupplierId, @SupplierName, @Unit);",
            new SqlParameter("@SKU", product.SKU),
            new SqlParameter("@Name", product.Name),
            new SqlParameter("@Category", product.Category),
            new SqlParameter("@CostPrice", product.CostPrice),
            new SqlParameter("@RetailPrice", product.RetailPrice),
            new SqlParameter("@StockQuantity", product.StockQuantity),
            new SqlParameter("@ReorderLevel", product.ReorderLevel),
            new SqlParameter("@SupplierId", (object?)product.SupplierId ?? DBNull.Value),
            new SqlParameter("@SupplierName", (object?)product.SupplierName ?? DBNull.Value),
            new SqlParameter("@Unit", product.Unit)
        );

        product.Id = dbId > 0 ? dbId : (Products.Count > 0 ? Products.Max(p => p.Id) + 1 : 1);
        Products.Insert(0, product);
        NotifyStateChanged();
    }

    public void UpdateProduct(ProductItem product)
    {
        var existing = Products.FirstOrDefault(p => p.Id == product.Id);
        if (existing != null)
        {
            existing.SKU = product.SKU;
            existing.Name = product.Name;
            existing.Category = product.Category;
            existing.CostPrice = product.CostPrice;
            existing.RetailPrice = product.RetailPrice;
            existing.StockQuantity = product.StockQuantity;
            existing.ReorderLevel = product.ReorderLevel;
            existing.SupplierId = product.SupplierId;
            existing.SupplierName = product.SupplierName;
            existing.Unit = product.Unit;

            ExecuteSqlNonQuery(
                "UPDATE dbo.Products SET SKU = @SKU, Name = @Name, Category = @Category, CostPrice = @CostPrice, " +
                "RetailPrice = @RetailPrice, StockQuantity = @StockQuantity, ReorderLevel = @ReorderLevel, " +
                "SupplierId = @SupplierId, SupplierName = @SupplierName, Unit = @Unit WHERE Id = @Id;",
                new SqlParameter("@Id", existing.Id),
                new SqlParameter("@SKU", existing.SKU),
                new SqlParameter("@Name", existing.Name),
                new SqlParameter("@Category", existing.Category),
                new SqlParameter("@CostPrice", existing.CostPrice),
                new SqlParameter("@RetailPrice", existing.RetailPrice),
                new SqlParameter("@StockQuantity", existing.StockQuantity),
                new SqlParameter("@ReorderLevel", existing.ReorderLevel),
                new SqlParameter("@SupplierId", (object?)existing.SupplierId ?? DBNull.Value),
                new SqlParameter("@SupplierName", (object?)existing.SupplierName ?? DBNull.Value),
                new SqlParameter("@Unit", existing.Unit)
            );

            NotifyStateChanged();
        }
    }

    public void AdjustProductStock(int id, int quantityDelta)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product != null)
        {
            product.StockQuantity = Math.Max(0, product.StockQuantity + quantityDelta);
            ExecuteSqlNonQuery("UPDATE dbo.Products SET StockQuantity = @StockQuantity WHERE Id = @Id;",
                new SqlParameter("@Id", id),
                new SqlParameter("@StockQuantity", product.StockQuantity));
            NotifyStateChanged();
        }
    }

    public void DeleteProduct(int id)
    {
        Products.RemoveAll(p => p.Id == id);
        ExecuteSqlNonQuery("DELETE FROM dbo.Products WHERE Id = @Id;", new SqlParameter("@Id", id));
        NotifyStateChanged();
    }

    public static List<ProductItem> GetDefaultProducts() => new()
    {
        new()
        {
            Id = 1,
            SKU = "SKU-MOR-01",
            Name = "Moroccan Argan Elixir Serum",
            Category = "Retail",
            CostPrice = 220,
            RetailPrice = 450,
            StockQuantity = 24,
            ReorderLevel = 5,
            SupplierId = 2,
            SupplierName = "Kerastase Luxury Supply",
            Unit = "Bottle"
        },
        new()
        {
            Id = 2,
            SKU = "SKU-KER-02",
            Name = "Keratin Smoothing Daily Shampoo",
            Category = "Retail",
            CostPrice = 310,
            RetailPrice = 650,
            StockQuantity = 18,
            ReorderLevel = 5,
            SupplierId = 1,
            SupplierName = "L'Oréal Professional PH",
            Unit = "Bottle"
        },
        new()
        {
            Id = 3,
            SKU = "SKU-TON-03",
            Name = "Botanical Scalp Detox Tonic",
            Category = "Retail",
            CostPrice = 180,
            RetailPrice = 350,
            StockQuantity = 12,
            ReorderLevel = 4,
            SupplierId = 2,
            SupplierName = "Kerastase Luxury Supply",
            Unit = "Bottle"
        },
        new()
        {
            Id = 4,
            SKU = "SKU-COL-04",
            Name = "Majirel Ash Blonde 8.1 Tube",
            Category = "Color",
            CostPrice = 380,
            RetailPrice = 750,
            StockQuantity = 4,
            ReorderLevel = 6,
            SupplierId = 1,
            SupplierName = "L'Oréal Professional PH",
            Unit = "Tube"
        },
        new()
        {
            Id = 5,
            SKU = "SKU-BLE-05",
            Name = "Blond Studio 9 Lightener (500g)",
            Category = "Color",
            CostPrice = 850,
            RetailPrice = 1600,
            StockQuantity = 3,
            ReorderLevel = 5,
            SupplierId = 1,
            SupplierName = "L'Oréal Professional PH",
            Unit = "Jar"
        }
    };
}
