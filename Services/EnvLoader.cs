using System;
using System.IO;

namespace SalonSuite.Services;

/// <summary>
/// Helper utility to load environment variables from local .env files into the current process environment.
/// </summary>
public static class EnvLoader
{
    private static bool _isLoaded = false;

    /// <summary>
    /// Loads key-value pairs from a .env file into Environment variables.
    /// Automatically discovers .env from the application base directory or current directory.
    /// </summary>
    public static void Load(string? customPath = null)
    {
        if (_isLoaded && string.IsNullOrEmpty(customPath))
            return;

        try
        {
            string? targetPath = customPath;

            if (string.IsNullOrEmpty(targetPath))
            {
                var candidatePaths = new[]
                {
                    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                    Path.Combine(AppContext.BaseDirectory, ".env"),
                    Path.GetFullPath(".env")
                };

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        targetPath = path;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(targetPath) || !File.Exists(targetPath))
            {
                return;
            }

            var lines = File.ReadAllLines(targetPath);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();

                // Skip blank lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#") || line.StartsWith("//"))
                    continue;

                var equalsIdx = line.IndexOf('=');
                if (equalsIdx <= 0)
                    continue;

                var key = line[..equalsIdx].Trim();
                var value = line[(equalsIdx + 1)..].Trim();

                // Strip bounding quotes if present
                if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                    (value.StartsWith("'") && value.EndsWith("'")))
                {
                    if (value.Length >= 2)
                        value = value[1..^1];
                }

                if (string.IsNullOrWhiteSpace(key))
                    continue;

                // Set the primary environment variable
                Environment.SetEnvironmentVariable(key, value);

                // Support ASP.NET Core hierarchical naming mappings:
                // 1) "Section__Key" <--> "Section:Key"
                if (key.Contains("__"))
                {
                    var colonKey = key.Replace("__", ":");
                    Environment.SetEnvironmentVariable(colonKey, value);
                }
                else if (key.Contains(':'))
                {
                    var underscoreKey = key.Replace(':', '_');
                    Environment.SetEnvironmentVariable(underscoreKey, value);
                }

                // 2) Standard uppercase aliases
                MapAlias(key, value, "FIREBASE_API_KEY", "Firebase:ApiKey", "Firebase__ApiKey");
                MapAlias(key, value, "FIREBASE_PROJECT_ID", "Firebase:ProjectId", "Firebase__ProjectId");
                MapAlias(key, value, "FIREBASE_CREDENTIALS_FILE", "Firebase:CredentialsFile", "Firebase__CredentialsFile");

                MapAlias(key, value, "CLOUDINARY_CLOUD_NAME", "Cloudinary:CloudName", "Cloudinary__CloudName");
                MapAlias(key, value, "CLOUDINARY_API_KEY", "Cloudinary:ApiKey", "Cloudinary__ApiKey");
                MapAlias(key, value, "CLOUDINARY_API_SECRET", "Cloudinary:ApiSecret", "Cloudinary__ApiSecret");

                MapAlias(key, value, "STRIPE_PUBLISHABLE_KEY", "Stripe:PublishableKey", "Stripe__PublishableKey");
                MapAlias(key, value, "STRIPE_SECRET_KEY", "Stripe:SecretKey", "Stripe__SecretKey");
                MapAlias(key, value, "STRIPE_WEBHOOK_SECRET", "Stripe:WebhookSecret", "Stripe__WebhookSecret");

                MapAlias(key, value, "XENDIT_SECRET_KEY", "Xendit:SecretKey", "Xendit__SecretKey");
                MapAlias(key, value, "XENDIT_PUBLIC_KEY", "Xendit:PublicKey", "Xendit__PublicKey");
                MapAlias(key, value, "XENDIT_WEBHOOK_TOKEN", "Xendit:WebhookToken", "Xendit__WebhookToken");

                MapAlias(key, value, "SMTP_HOST", "Smtp:Host", "Smtp__Host");
                MapAlias(key, value, "SMTP_PORT", "Smtp:Port", "Smtp__Port");
                MapAlias(key, value, "SMTP_ENABLE_SSL", "Smtp:EnableSsl", "Smtp__EnableSsl");
                MapAlias(key, value, "SMTP_SENDER_EMAIL", "Smtp:SenderEmail", "Smtp__SenderEmail");
                MapAlias(key, value, "SMTP_SENDER_NAME", "Smtp:SenderName", "Smtp__SenderName");
                MapAlias(key, value, "SMTP_USERNAME", "Smtp:Username", "Smtp__Username");
                MapAlias(key, value, "SMTP_PASSWORD", "Smtp:Password", "Smtp__Password");
            }

            _isLoaded = true;
            Console.WriteLine($"[EnvLoader] Loaded environment variables from {Path.GetFileName(targetPath)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EnvLoader] Notice: Could not load .env file: {ex.Message}");
        }
    }

    private static void MapAlias(string currentKey, string value, string uppercaseAlias, string colonKey, string doubleUnderscoreKey)
    {
        if (string.Equals(currentKey, uppercaseAlias, StringComparison.OrdinalIgnoreCase))
        {
            Environment.SetEnvironmentVariable(colonKey, value);
            Environment.SetEnvironmentVariable(doubleUnderscoreKey, value);
        }
    }
}
