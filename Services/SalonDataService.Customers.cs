using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<CustomerRecord> Customers { get; private set; } = new();

    private void LoadCustomersFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, FullName, Email, Phone, LoyaltyPoints, Tier, TotalSpent, VisitsCount, LastVisit, Notes, CreatedAt FROM dbo.Customers ORDER BY Id ASC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<CustomerRecord>();
            while (reader.Read())
            {
                list.Add(new CustomerRecord
                {
                    Id = reader.GetInt32(0),
                    FullName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Email = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Phone = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    LoyaltyPoints = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    Tier = reader.IsDBNull(5) ? "Bronze" : reader.GetString(5),
                    TotalSpent = reader.IsDBNull(6) ? 0m : reader.GetDecimal(6),
                    VisitsCount = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    LastVisit = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                    Notes = reader.IsDBNull(9) ? null : reader.GetString(9),
                    CreatedAt = reader.IsDBNull(10) ? DateTime.Now : reader.GetDateTime(10)
                });
            }
            if (list.Count > 0) Customers = list;
        }
        catch { }
    }

    public void AddCustomer(CustomerRecord customer)
    {
        customer.CreatedAt = DateTime.Now;
        customer.Tier = CalculateTier(customer.TotalSpent);

        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.Customers (FullName, Email, Phone, LoyaltyPoints, Tier, TotalSpent, VisitsCount, LastVisit, Notes, CreatedAt) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@FullName, @Email, @Phone, @LoyaltyPoints, @Tier, @TotalSpent, @VisitsCount, @LastVisit, @Notes, @CreatedAt);",
            new SqlParameter("@FullName", customer.FullName),
            new SqlParameter("@Email", (object?)customer.Email ?? DBNull.Value),
            new SqlParameter("@Phone", customer.Phone),
            new SqlParameter("@LoyaltyPoints", customer.LoyaltyPoints),
            new SqlParameter("@Tier", customer.Tier),
            new SqlParameter("@TotalSpent", customer.TotalSpent),
            new SqlParameter("@VisitsCount", customer.VisitsCount),
            new SqlParameter("@LastVisit", (object?)customer.LastVisit ?? DBNull.Value),
            new SqlParameter("@Notes", (object?)customer.Notes ?? DBNull.Value),
            new SqlParameter("@CreatedAt", customer.CreatedAt)
        );

        customer.Id = dbId > 0 ? dbId : (Customers.Count > 0 ? Customers.Max(c => c.Id) + 1 : 1);
        Customers.Add(customer);
        NotifyStateChanged();
    }

    public void UpdateCustomer(CustomerRecord customer)
    {
        var existing = Customers.FirstOrDefault(c => c.Id == customer.Id);
        if (existing != null)
        {
            existing.FullName = customer.FullName;
            existing.Phone = customer.Phone;
            existing.Email = customer.Email;
            existing.Notes = customer.Notes;
            existing.LoyaltyPoints = customer.LoyaltyPoints;
            existing.Tier = CalculateTier(existing.TotalSpent);

            ExecuteSqlNonQuery(
                "UPDATE dbo.Customers SET FullName = @FullName, Email = @Email, Phone = @Phone, " +
                "LoyaltyPoints = @LoyaltyPoints, Tier = @Tier, TotalSpent = @TotalSpent, VisitsCount = @VisitsCount, " +
                "Notes = @Notes, LastVisit = @LastVisit WHERE Id = @Id;",
                new SqlParameter("@Id", existing.Id),
                new SqlParameter("@FullName", existing.FullName),
                new SqlParameter("@Email", (object?)existing.Email ?? DBNull.Value),
                new SqlParameter("@Phone", existing.Phone),
                new SqlParameter("@LoyaltyPoints", existing.LoyaltyPoints),
                new SqlParameter("@Tier", existing.Tier),
                new SqlParameter("@TotalSpent", existing.TotalSpent),
                new SqlParameter("@VisitsCount", existing.VisitsCount),
                new SqlParameter("@Notes", (object?)existing.Notes ?? DBNull.Value),
                new SqlParameter("@LastVisit", (object?)existing.LastVisit ?? DBNull.Value)
            );

            NotifyStateChanged();
        }
    }

    public void DeleteCustomer(int id)
    {
        Customers.RemoveAll(c => c.Id == id);
        ExecuteSqlNonQuery("DELETE FROM dbo.Customers WHERE Id = @Id;", new SqlParameter("@Id", id));
        NotifyStateChanged();
    }

    public CustomerRecord EnsureCustomer(string name, string phone = "", string email = "", string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return null!;
        var trimmedName = name.Trim();
        var rawPhone = phone?.Trim() ?? string.Empty;
        var normalizedPhone = NormalizePhoneNumber(rawPhone);
        var trimmedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;

        CustomerRecord? existing = null;

        // 1. PRIMARY MATCH: Phone number
        if (!string.IsNullOrEmpty(normalizedPhone))
        {
            existing = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Phone) &&
                NormalizePhoneNumber(c.Phone) == normalizedPhone);
        }

        // 2. SECONDARY MATCH: Email address
        if (existing == null && !string.IsNullOrEmpty(trimmedEmail))
        {
            existing = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Email) &&
                c.Email.Trim().Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase));
        }

        // 3. FALLBACK MATCH: Exact Full Name
        if (existing == null && string.IsNullOrEmpty(normalizedPhone) && string.IsNullOrEmpty(trimmedEmail))
        {
            existing = Customers.FirstOrDefault(c =>
                string.IsNullOrEmpty(c.Phone) &&
                string.IsNullOrEmpty(c.Email) &&
                c.FullName.Equals(trimmedName, StringComparison.OrdinalIgnoreCase));
        }

        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(trimmedName) && !existing.FullName.Equals(trimmedName, StringComparison.OrdinalIgnoreCase))
                existing.FullName = trimmedName;
            if (string.IsNullOrWhiteSpace(existing.Phone) && !string.IsNullOrWhiteSpace(rawPhone))
                existing.Phone = rawPhone;
            if (string.IsNullOrWhiteSpace(existing.Email) && !string.IsNullOrWhiteSpace(trimmedEmail))
                existing.Email = trimmedEmail;
            if (string.IsNullOrWhiteSpace(existing.Notes) && !string.IsNullOrWhiteSpace(notes))
                existing.Notes = notes.Trim();

            UpdateCustomer(existing);
            return existing;
        }

        // Create new customer and save to database
        var newCust = new CustomerRecord
        {
            FullName = trimmedName,
            Phone = rawPhone,
            Email = trimmedEmail,
            Notes = notes?.Trim(),
            LoyaltyPoints = 0,
            Tier = "Bronze",
            TotalSpent = 0,
            VisitsCount = 0,
            CreatedAt = DateTime.Now
        };
        AddCustomer(newCust);
        return newCust;
    }

    public CustomerRecord? FindCustomerByPhoneOrName(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        EnsureSeedData();

        var trimmed = query.Trim();
        var normalizedQueryPhone = NormalizePhoneNumber(trimmed);

        // 1. Match by Phone Number if numeric
        if (!string.IsNullOrEmpty(normalizedQueryPhone) && normalizedQueryPhone.Length >= 4)
        {
            var byPhone = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Phone) &&
                NormalizePhoneNumber(c.Phone).Contains(normalizedQueryPhone));
            if (byPhone != null) return byPhone;
        }

        // 2. Exact Full Name match
        var byExactName = Customers.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.FullName) &&
            c.FullName.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byExactName != null) return byExactName;

        // 3. Partial or substring Full Name match
        var byPartialName = Customers.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.FullName) &&
            (c.FullName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
             trimmed.Contains(c.FullName, StringComparison.OrdinalIgnoreCase)));
        if (byPartialName != null) return byPartialName;

        // 4. Word-by-word match
        var searchWords = trimmed.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (searchWords.Length > 0)
        {
            var byWords = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.FullName) &&
                searchWords.All(w => c.FullName.Contains(w, StringComparison.OrdinalIgnoreCase)));
            if (byWords != null) return byWords;
        }

        // 5. Match by Email
        var byEmail = Customers.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.Email) &&
            c.Email.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byEmail != null) return byEmail;

        // 6. Match by Client Code
        var byCode = Customers.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.ClientCode) &&
            c.ClientCode.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byCode != null) return byCode;

        // 7. Match by raw phone string
        return Customers.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.Phone) &&
            c.Phone.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static string NormalizePhoneNumber(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("63") && digits.Length >= 12)
        {
            digits = "0" + digits.Substring(2);
        }
        return digits;
    }

    public static string CalculateTier(decimal totalSpent) => totalSpent switch
    {
        >= 25000 => "Platinum",
        >= 10000 => "Gold",
        >= 3500 => "Silver",
        _ => "Bronze"
    };
}
