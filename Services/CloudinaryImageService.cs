using System;
using System.IO;
using System.Threading.Tasks;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SalonSuite.Services;

/// <summary>
/// Cloudinary Image Storage Service.
/// Handles fast cloud upload, automatic compression/format optimization (WebP), 
/// and returns secure CDN image URLs.
/// </summary>
public class CloudinaryImageService
{
    private readonly Cloudinary? _cloudinary;
    private readonly ILogger<CloudinaryImageService>? _logger;
    private readonly bool _isConfigured;

    public bool IsConfigured => _isConfigured;

    public CloudinaryImageService(IConfiguration configuration, ILogger<CloudinaryImageService>? logger = null)
    {
        _logger = logger;

        var cloudName = configuration["Cloudinary:CloudName"]?.Trim();
        var apiKey = configuration["Cloudinary:ApiKey"]?.Trim();
        var apiSecret = configuration["Cloudinary:ApiSecret"]?.Trim();

        if (!string.IsNullOrEmpty(cloudName) &&
            !string.IsNullOrEmpty(apiKey) &&
            !string.IsNullOrEmpty(apiSecret) &&
            !cloudName.StartsWith("YOUR_") &&
            !apiKey.StartsWith("YOUR_"))
        {
            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
            _cloudinary.Api.Secure = true;
            _isConfigured = true;
            _logger?.LogInformation("Cloudinary service initialized successfully for cloud: {CloudName}", cloudName);
        }
        else
        {
            _isConfigured = false;
            _logger?.LogWarning("Cloudinary is not fully configured in appsettings.json (using placeholders). Image uploads will require valid credentials.");
        }
    }

    /// <summary>
    /// Uploads an image from a Blazor IBrowserFile to Cloudinary.
    /// Returns the secure HTTPS URL on success, or null on failure.
    /// </summary>
    public async Task<string?> UploadImageAsync(IBrowserFile file, string folder = "salonsuite", int maxSizeBytes = 10 * 1024 * 1024)
    {
        if (!_isConfigured || _cloudinary == null)
        {
            _logger?.LogError("Cannot upload to Cloudinary: Cloudinary credentials are missing or invalid in appsettings.json.");
            return null;
        }

        try
        {
            using var stream = file.OpenReadStream(maxAllowedSize: maxSizeBytes);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.Name, memoryStream),
                Folder = folder,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.StatusCode == System.Net.HttpStatusCode.OK)
            {
                _logger?.LogInformation("Image uploaded successfully to Cloudinary: {Url}", uploadResult.SecureUrl);
                return uploadResult.SecureUrl?.ToString();
            }
            else
            {
                _logger?.LogError("Cloudinary upload failed: {Error}", uploadResult.Error?.Message);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during Cloudinary upload for file: {FileName}", file.Name);
            return null;
        }
    }

    /// <summary>
    /// Uploads an image stream directly to Cloudinary.
    /// </summary>
    public async Task<string?> UploadStreamAsync(Stream stream, string fileName, string folder = "salonsuite")
    {
        if (!_isConfigured || _cloudinary == null)
        {
            _logger?.LogError("Cannot upload to Cloudinary: Cloudinary credentials are missing or invalid.");
            return null;
        }

        try
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, stream),
                Folder = folder,
                Transformation = new Transformation()
                    .Quality("auto")
                    .FetchFormat("auto")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl?.ToString();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during Cloudinary stream upload for file: {FileName}", fileName);
            return null;
        }
    }

    /// <summary>
    /// Deletes an image from Cloudinary using its Public ID.
    /// </summary>
    public async Task<bool> DeleteImageAsync(string publicId)
    {
        if (!_isConfigured || _cloudinary == null) return false;

        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);
            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception during Cloudinary image deletion for publicId: {PublicId}", publicId);
            return false;
        }
    }
}
