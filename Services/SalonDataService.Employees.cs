using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using SalonSuite.Models;

namespace SalonSuite.Services;

public partial class SalonDataService
{
    public List<TeamMemberItem> Team { get; private set; } = new();

    public List<TeamMemberItem> Stylists => Team.Where(t =>
        t.IsActive &&
        (t.Role.Contains("Stylist", StringComparison.OrdinalIgnoreCase) ||
         t.Role.Contains("Color Specialist", StringComparison.OrdinalIgnoreCase) ||
         t.Role.Contains("Hair Specialist", StringComparison.OrdinalIgnoreCase)) &&
        !t.Role.Contains("Cashier", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Front Desk", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Admin", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Owner", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Manager", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Staff", StringComparison.OrdinalIgnoreCase)
    ).ToList();

    private async Task LoadEmployeesFromFirestoreAsync()
    {
        if (!_dbConnected || _firestoreDb == null) return;

        try
        {
            var snapshot = await _firestoreDb.Collection("employees").GetSnapshotAsync();
            var list = new List<TeamMemberItem>();
            foreach (var doc in snapshot.Documents)
            {
                if (doc.Exists)
                {
                    var item = doc.ConvertTo<TeamMemberItem>();
                    if (item.Id == 0 && int.TryParse(doc.Id, out int parsedId))
                    {
                        item.Id = parsedId;
                    }
                    list.Add(item);
                }
            }
            if (list.Count > 0)
            {
                Team = list.OrderBy(t => t.Id).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error loading employees: {ex.Message}");
        }
    }

    public void AddTeamMember(TeamMemberItem member)
    {
        member.Id = Team.Count > 0 ? Team.Max(t => t.Id) + 1 : 1;
        Team.Add(member);
        NotifyStateChanged();

        RunBackgroundTask(async () => await SaveDocAsync("employees", member.Id.ToString(), member));
    }

    public void UpdateTeamMember(TeamMemberItem member)
    {
        var existing = Team.FirstOrDefault(t => t.Id == member.Id);
        if (existing != null)
        {
            existing.Name = member.Name;
            existing.Role = member.Role;
            existing.Specialization = member.Specialization;
            existing.CommissionRate = member.CommissionRate;
            existing.Email = member.Email;
            existing.Phone = member.Phone;
            existing.ImageUrl = member.ImageUrl;
            existing.IsActive = member.IsActive;

            NotifyStateChanged();

            RunBackgroundTask(async () => await SaveDocAsync("employees", existing.Id.ToString(), existing));
        }
    }

    public void DeleteTeamMember(int id)
    {
        Team.RemoveAll(t => t.Id == id);
        NotifyStateChanged();
        RunBackgroundTask(async () => await DeleteDocAsync("employees", id.ToString()));
    }

    public TeamMemberItem EnsureEmployeeExists(string name, string email, string role = "Senior Stylist")
    {
        if (string.IsNullOrWhiteSpace(email)) return null!;
        EnsureSeedData();

        var existing = Team.FirstOrDefault(t => t.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            if (!string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(existing.Name))
            {
                existing.Name = name;
                UpdateTeamMember(existing);
            }
            return existing;
        }

        var newMember = new TeamMemberItem
        {
            Name = string.IsNullOrWhiteSpace(name) ? email.Split('@')[0] : name,
            Email = email.Trim().ToLowerInvariant(),
            Role = role,
            Specialization = role.Contains("Cashier") ? "POS Register & Front Desk" : "Hair Styling & Care",
            CommissionRate = role.Contains("Cashier") ? 0.10m : 0.25m,
            ImageUrl = role.Contains("Cashier")
                ? "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=500&auto=format&fit=crop&q=80"
                : "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=500&auto=format&fit=crop&q=80",
            IsActive = true
        };
        AddTeamMember(newMember);
        return newMember;
    }

    public static List<TeamMemberItem> GetDefaultTeam() => new()
    {
        new()
        {
            Id = 1,
            Name = "Sofia Martinez",
            Role = "Creative Director & Master Stylist",
            Specialization = "Color Architecture & Balayage",
            CommissionRate = 0.30m,
            Email = "sofia@beautyhair.com",
            Phone = "+63 917 123 4567",
            ImageUrl = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=500&auto=format&fit=crop&q=80",
            IsActive = true
        },
        new()
        {
            Id = 2,
            Name = "James Anderson",
            Role = "Senior Color Specialist",
            Specialization = "Blonde Transformations & Toning",
            CommissionRate = 0.25m,
            Email = "james@beautyhair.com",
            Phone = "+63 918 234 5678",
            ImageUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=500&auto=format&fit=crop&q=80",
            IsActive = true
        },
        new()
        {
            Id = 3,
            Name = "Isabella Chen",
            Role = "Master Stylist & Updo Architect",
            Specialization = "Precision Cutting & Bridal Styling",
            CommissionRate = 0.25m,
            Email = "isabella@beautyhair.com",
            Phone = "+63 920 345 6789",
            ImageUrl = "https://images.unsplash.com/photo-1517841905240-472988babdf9?w=500&auto=format&fit=crop&q=80",
            IsActive = true
        },
        new()
        {
            Id = 4,
            Name = "Clara Santos",
            Role = "Front Desk & Head Cashier",
            Specialization = "POS Register & Client Reception",
            CommissionRate = 0.10m,
            Email = "clara@beautyhair.com",
            Phone = "+63 917 888 9900",
            ImageUrl = "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?w=500&auto=format&fit=crop&q=80",
            IsActive = true
        }
    };
}
