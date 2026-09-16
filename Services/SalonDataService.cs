using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using SalonSuite.Models;

namespace SalonSuite.Services;

/// <summary>
/// Core SalonDataService partial class managing Firebase Cloud Firestore synchronization, 
/// state notifications, and user authentication session.
/// </summary>
public partial class SalonDataService
{
    public event Action? OnChange;

    private readonly FirestoreDb? _firestoreDb;
    private readonly bool _dbConnected = false;
    private readonly string _projectId;

    public UserSession CurrentUser { get; private set; } = new();
    public bool IsDatabaseConnected => _dbConnected;
    public FirestoreDb? Firestore => _firestoreDb;

    public SalonDataService(IConfiguration? configuration = null)
    {
        _projectId = configuration?["Firebase:ProjectId"] ?? "beautysalonsuite";
        string credFile = configuration?["Firebase:CredentialsFile"] ?? "firebase-adminsdk.json";

        try
        {
            // Search for firebase-adminsdk.json in standard directories
            string[] searchPaths =
            {
                Path.Combine(AppContext.BaseDirectory, credFile),
                Path.Combine(Directory.GetCurrentDirectory(), credFile),
                Path.GetFullPath(credFile)
            };

            string? resolvedCredPath = searchPaths.FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(resolvedCredPath))
            {
                Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", resolvedCredPath);
                
                try
                {
                    var jsonContent = File.ReadAllText(resolvedCredPath);
                    using var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                    if (jsonDoc.RootElement.TryGetProperty("project_id", out var projProp) && !string.IsNullOrWhiteSpace(projProp.GetString()))
                    {
                        _projectId = projProp.GetString()!;
                    }
                }
                catch { }

                var builder = new FirestoreDbBuilder
                {
                    ProjectId = _projectId,
                    ConverterRegistry = new ConverterRegistry { new FirestoreDecimalConverter() }
                };
                _firestoreDb = builder.Build();
                _dbConnected = true;
                Console.WriteLine($"[Firebase] Firestore connected successfully to project '{_projectId}' with credentials: {resolvedCredPath}");
            }
            else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS")))
            {
                var builder = new FirestoreDbBuilder
                {
                    ProjectId = _projectId,
                    ConverterRegistry = new ConverterRegistry { new FirestoreDecimalConverter() }
                };
                _firestoreDb = builder.Build();
                _dbConnected = true;
                Console.WriteLine("[Firebase] Firestore connected using GOOGLE_APPLICATION_CREDENTIALS environment variable.");
            }
            else
            {
                _dbConnected = false;
                Console.WriteLine("[Firebase] firebase-adminsdk.json not found. Operating in local in-memory mode with seed data.");
            }
        }
        catch (Exception ex)
        {
            _dbConnected = false;
            Console.WriteLine($"[Firebase] Firestore initialization notice: {ex.Message}. Falling back to in-memory mode.");
        }

        // Initialize from Firebase Firestore or seed data
        InitializeDatabaseOrSeed();
    }

    public void NotifyStateChanged() => OnChange?.Invoke();

    #region Firebase Firestore Initialization & Sync
    public void InitializeDatabaseOrSeed()
    {
        if (_dbConnected && _firestoreDb != null)
        {
            try
            {
                // Synchronously wait for initial load during startup
                Task.Run(async () => await LoadAllFromFirestoreAsync()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Firebase] Initial Firestore sync warning: {ex.Message}");
                if (Customers.Count == 0)
                {
                    SeedData();
                }
            }
        }
        else
        {
            if (Customers.Count == 0)
            {
                SeedData();
            }
        }
    }

    private async Task LoadAllFromFirestoreAsync()
    {
        await LoadCustomersFromFirestoreAsync();
        await LoadEmployeesFromFirestoreAsync();
        await LoadSuppliersFromFirestoreAsync();
        await LoadServicesFromFirestoreAsync();
        await LoadProductsFromFirestoreAsync();
        await LoadPromotionsFromFirestoreAsync();
        await LoadLoyaltyRewardsFromFirestoreAsync();
        await LoadAppointmentsFromFirestoreAsync();
        await LoadInvoicesFromFirestoreAsync();

        // If Firestore is freshly connected and has no records, populate it with seed data
        if (Customers.Count == 0 && Appointments.Count == 0 && Services.Count == 0)
        {
            SeedData();
            await PersistInitialSeedToFirestoreAsync();
        }
        else
        {
            InitStaticContent();
            SyncAllDataRelationships();
        }

        NotifyStateChanged();
    }

    public void EnsureSeedData()
    {
        if (Customers == null || Customers.Count == 0)
        {
            InitializeDatabaseOrSeed();
            NotifyStateChanged();
        }
    }

    /// <summary>
    /// Fire-and-forget helper to execute Firestore document operations without blocking UI
    /// </summary>
    public void RunBackgroundTask(Func<Task> asyncAction)
    {
        if (!_dbConnected || _firestoreDb == null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await asyncAction();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Firebase] Background operation error: {ex.Message}");
            }
        });
    }

    public async Task SaveDocAsync<T>(string collectionName, string documentId, T data)
    {
        if (!_dbConnected || _firestoreDb == null) return;
        try
        {
            var docRef = _firestoreDb.Collection(collectionName).Document(documentId);
            await docRef.SetAsync(data, SetOptions.Overwrite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error saving {collectionName}/{documentId}: {ex.Message}");
        }
    }

    public async Task DeleteDocAsync(string collectionName, string documentId)
    {
        if (!_dbConnected || _firestoreDb == null) return;
        try
        {
            var docRef = _firestoreDb.Collection(collectionName).Document(documentId);
            await docRef.DeleteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Firebase] Error deleting {collectionName}/{documentId}: {ex.Message}");
        }
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

