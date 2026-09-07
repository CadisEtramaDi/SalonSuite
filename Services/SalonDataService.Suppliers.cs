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

    private void LoadSuppliersFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, CompanyName, ContactPerson, Email, Phone, Address, SuppliedCategory, PaymentTerms, IsActive FROM dbo.Suppliers ORDER BY Id ASC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<SupplierRecord>();
            while (reader.Read())
            {
                list.Add(new SupplierRecord
                {
                    Id = reader.GetInt32(0),
                    CompanyName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    ContactPerson = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Email = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Phone = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    Address = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    SuppliedCategory = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    PaymentTerms = reader.IsDBNull(7) ? "Net 30" : reader.GetString(7),
                    IsActive = reader.IsDBNull(8) || reader.GetBoolean(8)
                });
            }
            if (list.Count > 0) Suppliers = list;
        }
        catch { }
    }

    public void AddSupplier(SupplierRecord supplier)
    {
        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.Suppliers (CompanyName, ContactPerson, Email, Phone, Address, SuppliedCategory, PaymentTerms, IsActive) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@CompanyName, @ContactPerson, @Email, @Phone, @Address, @SuppliedCategory, @PaymentTerms, @IsActive);",
            new SqlParameter("@CompanyName", supplier.CompanyName),
            new SqlParameter("@ContactPerson", supplier.ContactPerson),
            new SqlParameter("@Email", (object?)supplier.Email ?? DBNull.Value),
            new SqlParameter("@Phone", supplier.Phone),
            new SqlParameter("@Address", (object?)supplier.Address ?? DBNull.Value),
            new SqlParameter("@SuppliedCategory", supplier.SuppliedCategory),
            new SqlParameter("@PaymentTerms", supplier.PaymentTerms),
            new SqlParameter("@IsActive", supplier.IsActive)
        );

        supplier.Id = dbId > 0 ? dbId : (Suppliers.Count > 0 ? Suppliers.Max(s => s.Id) + 1 : 1);
        Suppliers.Add(supplier);
        NotifyStateChanged();
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

            ExecuteSqlNonQuery(
                "UPDATE dbo.Suppliers SET CompanyName = @CompanyName, ContactPerson = @ContactPerson, Email = @Email, " +
                "Phone = @Phone, Address = @Address, SuppliedCategory = @SuppliedCategory, PaymentTerms = @PaymentTerms, " +
                "IsActive = @IsActive WHERE Id = @Id;",
                new SqlParameter("@Id", existing.Id),
                new SqlParameter("@CompanyName", existing.CompanyName),
                new SqlParameter("@ContactPerson", existing.ContactPerson),
                new SqlParameter("@Email", (object?)existing.Email ?? DBNull.Value),
                new SqlParameter("@Phone", existing.Phone),
                new SqlParameter("@Address", (object?)existing.Address ?? DBNull.Value),
                new SqlParameter("@SuppliedCategory", existing.SuppliedCategory),
                new SqlParameter("@PaymentTerms", existing.PaymentTerms),
                new SqlParameter("@IsActive", existing.IsActive)
            );

            NotifyStateChanged();
        }
    }

    public void DeleteSupplier(int id)
    {
        Suppliers.RemoveAll(s => s.Id == id);
        ExecuteSqlNonQuery("DELETE FROM dbo.Suppliers WHERE Id = @Id;", new SqlParameter("@Id", id));
        NotifyStateChanged();
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
