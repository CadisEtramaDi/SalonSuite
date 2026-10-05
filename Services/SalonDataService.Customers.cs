using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<CustomerRecord> Customers { get; private set; } = new();

    private async Task LoadCustomersFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("customers").GetSnapshotAsync();
            var list = new List<CustomerRecord>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<CustomerRecord>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Customers = list.OrderBy(c => c.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading customers: {ex.Message}");
        }
    }

    public void AddCustomer(CustomerRecord customer)
    {
        customer.CreatedAt = DateTime.UtcNow;
        customer.Tier = CalculateTier(customer.TotalSpent);
        customer.Id = Customers.Count > 0 ? Customers.Max(c => c.Id) + 1 : 1;

        if (customer.LastVisit.HasValue && customer.LastVisit.Value.Kind != DateTimeKind.Utc)
        {
            customer.LastVisit = DateTime.SpecifyKind(customer.LastVisit.Value, DateTimeKind.Utc);
        }

        Customers.Add(customer);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("customers", customer.Id.ToString(), customer));
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
            existing.TotalSpent = customer.TotalSpent;
            existing.VisitsCount = customer.VisitsCount;
            existing.LastVisit = customer.LastVisit.HasValue && customer.LastVisit.Value.Kind != DateTimeKind.Utc
                ? DateTime.SpecifyKind(customer.LastVisit.Value, DateTimeKind.Utc)
                : customer.LastVisit;
            existing.Tier = CalculateTier(existing.TotalSpent);

            if (existing.CreatedAt.Kind != DateTimeKind.Utc)
            {
                existing.CreatedAt = DateTime.SpecifyKind(existing.CreatedAt, DateTimeKind.Utc);
            }

            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("customers", existing.Id.ToString(), existing));
        }
    }

    public void DeleteCustomer(int id)
    {
        Customers.RemoveAll(c => c.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("customers", id.ToString()));
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
            if (!string.IsNullOrWhiteSpace(rawPhone) && (string.IsNullOrWhiteSpace(existing.Phone) || existing.Phone != rawPhone))
                existing.Phone = rawPhone;
            if (!string.IsNullOrWhiteSpace(trimmedEmail) && (string.IsNullOrWhiteSpace(existing.Email) || !existing.Email.Equals(trimmedEmail, StringComparison.OrdinalIgnoreCase)))
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
            CreatedAt = DateTime.UtcNow
        };
        AddCustomer(newCust);
        return newCust;
    }

    public CustomerRecord? FindCustomerByPhoneOrName(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        EnsureSeedData();

        var trimmed = query.Trim();

        // 1. If query contains '@', match by Email first
        if (trimmed.Contains('@'))
        {
            var byEmail = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Email) &&
                c.Email.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (byEmail != null) return byEmail;

            var byPartialEmail = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Email) &&
                c.Email.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
            if (byPartialEmail != null) return byPartialEmail;
        }

        // 2. If query starts with "VIP-", match by Client Code
        if (trimmed.StartsWith("VIP-", StringComparison.OrdinalIgnoreCase))
        {
            var byCode = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.ClientCode) &&
                c.ClientCode.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
            if (byCode != null) return byCode;
        }

        // 3. Match by Phone Number if numeric digits exist
        var normalizedQueryPhone = NormalizePhoneNumber(trimmed);
        if (!string.IsNullOrEmpty(normalizedQueryPhone) && normalizedQueryPhone.Length >= 4)
        {
            var byPhone = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.Phone) &&
                NormalizePhoneNumber(c.Phone).Contains(normalizedQueryPhone));
            if (byPhone != null) return byPhone;
        }

        // 4. Exact Full Name match
        var byExactName = Customers.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.FullName) &&
            c.FullName.Trim().Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byExactName != null) return byExactName;

        // 5. Partial or substring Full Name match (only if not an email)
        if (!trimmed.Contains('@'))
        {
            var byPartialName = Customers.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.FullName) &&
                (c.FullName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                 trimmed.Contains(c.FullName, StringComparison.OrdinalIgnoreCase)));
            if (byPartialName != null) return byPartialName;

            var searchWords = trimmed.Split(new[] { ' ', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (searchWords.Length > 0)
            {
                var byWords = Customers.FirstOrDefault(c =>
                    !string.IsNullOrEmpty(c.FullName) &&
                    searchWords.All(w => c.FullName.Contains(w, StringComparison.OrdinalIgnoreCase)));
                if (byWords != null) return byWords;
            }
        }

        // 6. Match by Client Code fallback
        var byCodeFallback = Customers.FirstOrDefault(c =>
            !string.IsNullOrEmpty(c.ClientCode) &&
            c.ClientCode.Contains(trimmed, StringComparison.OrdinalIgnoreCase));
        if (byCodeFallback != null) return byCodeFallback;

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
