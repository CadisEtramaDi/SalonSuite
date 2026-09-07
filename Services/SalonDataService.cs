using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalonSuite.Models;

namespace SalonSuite.Services;

/// <summary>
/// Core SalonDataService partial class managing database connection, state synchronization, 
/// and user authentication session. Modular entity features are decomposed into partial class files.
/// </summary>
public partial class SalonDataService
{
    public event Action? OnChange;

    private readonly string _connectionString;
    private bool _dbConnected = false;

    public UserSession CurrentUser { get; private set; } = new();
    public bool IsDatabaseConnected => _dbConnected;

    public SalonDataService(IConfiguration? configuration = null)
    {
        _connectionString = configuration?.GetConnectionString("DefaultConnection") 
            ?? "Server=.\\SQLEXPRESS;Database=SalonSuiteDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";

        // Initialize from SQL Server database or fall back to seed data
        InitializeDatabaseOrSeed();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();

    #region Database Initialization & Sync
    public void InitializeDatabaseOrSeed()
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            _dbConnected = true;

            // Load records from SQL Server tables
            LoadCustomersFromDb(conn);
            LoadEmployeesFromDb(conn);
            LoadSuppliersFromDb(conn);
            LoadServicesFromDb(conn);
            LoadProductsFromDb(conn);
            LoadPromotionsFromDb(conn);
            LoadLoyaltyRewardsFromDb(conn);
            LoadAppointmentsFromDb(conn);
            LoadInvoicesFromDb(conn);

            // If SQL Server database is freshly created and empty, populate with seed data
            if (Customers.Count == 0 && Appointments.Count == 0 && Services.Count == 0)
            {
                SeedData();
                PersistInitialSeedToDb(conn);
            }
            else
            {
                // Populate static packages / reviews if not in DB
                InitStaticContent();
                SyncAllDataRelationships();
            }
        }
        catch (Exception)
        {
            _dbConnected = false;
            if (Customers.Count == 0)
            {
                SeedData();
            }
        }
    }

    public void EnsureSeedData()
    {
        if (Customers == null || Customers.Count == 0)
        {
            InitializeDatabaseOrSeed();
            NotifyStateChanged();
        }
    }

    private int ExecuteSqlScalar(string sql, params SqlParameter[] parameters)
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters);
            var result = cmd.ExecuteScalar();
            if (result != null && int.TryParse(result.ToString(), out int id))
            {
                return id;
            }
        }
        catch { }
        return 0;
    }

    private void ExecuteSqlNonQuery(string sql, params SqlParameter[] parameters)
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters);
            cmd.ExecuteNonQuery();
        }
        catch { }
    }
    #endregion

    #region Authentication & Session
    public void LoginAs(string role, string name, string email)
    {
        CurrentUser = new UserSession
        {
            IsLoggedIn = true,
            Role = role,
            Name = name,
            Email = email
        };
        NotifyStateChanged();
    }

    public void Logout()
    {
        CurrentUser = new UserSession();
        NotifyStateChanged();
    }
    #endregion
}
