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

    private async Task LoadProductsFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("products").GetSnapshotAsync();
            var list = new List<ProductItem>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<ProductItem>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Products = list.OrderByDescending(p => p.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading products: {ex.Message}");
        }
    }

    public void AddProduct(ProductItem product)
    {
        if (string.IsNullOrWhiteSpace(product.SKU))
        {
            product.SKU = $"SKU-PRD-{Products.Count + 1:D3}";
        }

        product.Id = Products.Count > 0 ? Products.Max(p => p.Id) + 1 : 1;
        Products.Insert(0, product);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("products", product.Id.ToString(), product));
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
            existing.ImageUrl = product.ImageUrl;

            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("products", existing.Id.ToString(), existing));
        }
    }

    public void AdjustProductStock(int id, int quantityDelta)
    {
        var product = Products.FirstOrDefault(p => p.Id == id);
        if (product != null)
        {
            product.StockQuantity = Math.Max(0, product.StockQuantity + quantityDelta);
            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("products", product.Id.ToString(), product));
        }
    }

    public void DeleteProduct(int id)
    {
        Products.RemoveAll(p => p.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("products", id.ToString()));
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
