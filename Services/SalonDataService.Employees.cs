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
        !t.Role.Contains("Cashier", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Front Desk", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Admin", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Owner", StringComparison.OrdinalIgnoreCase) &&
        !t.Role.Contains("Manager", StringComparison.OrdinalIgnoreCase)
    ).ToList() is { Count: > 0 } list ? list : Team;

    private void LoadEmployeesFromDb(SqlConnection conn)
    {
        try
        {
            using var cmd = new SqlCommand("SELECT Id, Name, Role, Specialization, CommissionRate, Phone, Email, ImageUrl, IsActive FROM dbo.Employees ORDER BY Id ASC", conn);
            using var reader = cmd.ExecuteReader();
            var list = new List<TeamMemberItem>();
            while (reader.Read())
            {
                list.Add(new TeamMemberItem
                {
                    Id = reader.GetInt32(0),
                    Name = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Role = reader.IsDBNull(2) ? "Senior Stylist" : reader.GetString(2),
                    Specialization = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    CommissionRate = reader.IsDBNull(4) ? 0.25m : reader.GetDecimal(4),
                    Phone = reader.IsDBNull(5) ? "" : reader.GetString(5),
                    Email = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    ImageUrl = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    IsActive = reader.IsDBNull(8) || reader.GetBoolean(8)
                });
            }
            if (list.Count > 0) Team = list;
        }
        catch { }
    }

    public void AddTeamMember(TeamMemberItem member)
    {
        int dbId = ExecuteSqlScalar(
            "INSERT INTO dbo.Employees (Name, Role, Specialization, CommissionRate, Phone, Email, ImageUrl, IsActive) " +
            "OUTPUT INSERTED.Id " +
            "VALUES (@Name, @Role, @Specialization, @CommissionRate, @Phone, @Email, @ImageUrl, @IsActive);",
            new SqlParameter("@Name", member.Name),
            new SqlParameter("@Role", member.Role),
            new SqlParameter("@Specialization", member.Specialization),
            new SqlParameter("@CommissionRate", member.CommissionRate),
            new SqlParameter("@Phone", (object?)member.Phone ?? DBNull.Value),
            new SqlParameter("@Email", (object?)member.Email ?? DBNull.Value),
            new SqlParameter("@ImageUrl", (object?)member.ImageUrl ?? DBNull.Value),
            new SqlParameter("@IsActive", member.IsActive)
        );

        member.Id = dbId > 0 ? dbId : (Team.Count > 0 ? Team.Max(t => t.Id) + 1 : 1);
        Team.Add(member);
        NotifyStateChanged();
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

            ExecuteSqlNonQuery(
                "UPDATE dbo.Employees SET Name = @Name, Role = @Role, Specialization = @Specialization, " +
                "CommissionRate = @CommissionRate, Phone = @Phone, Email = @Email, ImageUrl = @ImageUrl, IsActive = @IsActive " +
                "WHERE Id = @Id;",
                new SqlParameter("@Id", existing.Id),
                new SqlParameter("@Name", existing.Name),
                new SqlParameter("@Role", existing.Role),
                new SqlParameter("@Specialization", existing.Specialization),
                new SqlParameter("@CommissionRate", existing.CommissionRate),
                new SqlParameter("@Phone", (object?)existing.Phone ?? DBNull.Value),
                new SqlParameter("@Email", (object?)existing.Email ?? DBNull.Value),
                new SqlParameter("@ImageUrl", (object?)existing.ImageUrl ?? DBNull.Value),
                new SqlParameter("@IsActive", existing.IsActive)
            );

            NotifyStateChanged();
        }
    }

    public void DeleteTeamMember(int id)
    {
        Team.RemoveAll(t => t.Id == id);
        ExecuteSqlNonQuery("DELETE FROM dbo.Employees WHERE Id = @Id;", new SqlParameter("@Id", id));
        NotifyStateChanged();
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
