using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<SupplierRecord> Suppliers { get; private set; } = new();

    private async Task LoadSuppliersFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("suppliers").GetSnapshotAsync();
            var list = new List<SupplierRecord>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<SupplierRecord>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Suppliers = list.OrderBy(s => s.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading suppliers: {ex.Message}");
        }
    }

    public void AddSupplier(SupplierRecord supplier)
    {
        supplier.Id = Suppliers.Count > 0 ? Suppliers.Max(s => s.Id) + 1 : 1;
        Suppliers.Add(supplier);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("suppliers", supplier.Id.ToString(), supplier));
    }

    public void UpdateSupplier(SupplierRecord supplier)
    {
        var existing = Suppliers.FirstOrDefault(s => s.Id == supplier.Id);
        if (existing != null)
        {
            existing.CompanyName = supplier.CompanyName;
            existing.ContactPerson = supplier.ContactPerson;
            existing.Email = supplier.Email;
            existing.Phone = supplier.Phone;
            existing.Address = supplier.Address;
            existing.SuppliedCategory = supplier.SuppliedCategory;
            existing.PaymentTerms = supplier.PaymentTerms;
            existing.IsActive = supplier.IsActive;

            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("suppliers", existing.Id.ToString(), existing));
        }
    }

    public void DeleteSupplier(int id)
    {
        Suppliers.RemoveAll(s => s.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("suppliers", id.ToString()));
    }

    public static List<SupplierRecord> GetDefaultSuppliers() => new()
    {
        new()
        {
            Id = 1,
            CompanyName = "L'Oréal Professional PH",
            ContactPerson = "Marcus Vance",
            Email = "orders@loreal.ph",
            Phone = "+63 917 111 2233",
            Address = "Bonifacio Global City, Taguig",
            SuppliedCategory = "Hair Color & Treatments",
            PaymentTerms = "Net 30",
            IsActive = true
        },
        new()
        {
            Id = 2,
            CompanyName = "Kerastase Luxury Supply",
            ContactPerson = "Elena Rostova",
            Email = "elena@kerastase.ph",
            Phone = "+63 918 222 3344",
            Address = "Makati City, Metro Manila",
            SuppliedCategory = "Premium Serums & Shampoos",
            PaymentTerms = "Net 15",
            IsActive = true
        },
        new()
        {
            Id = 3,
            CompanyName = "Dyson Pro Tools Manila",
            ContactPerson = "David Ang",
            Email = "sales@dysonpro.ph",
            Phone = "+63 920 333 4455",
            Address = "Ortigas Center, Pasig City",
            SuppliedCategory = "Hair Dryers & Salon Tools",
            PaymentTerms = "COD",
            IsActive = true
        }
    };
}
