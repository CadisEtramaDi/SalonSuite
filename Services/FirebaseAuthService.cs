using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace SalonSuite.Services;

public class FirebaseAuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? IdToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? LocalId { get; set; }
    public string? Email { get; set; }
}

public class FirebaseSignInRequest
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("returnSecureToken")]
    public bool ReturnSecureToken { get; set; } = true;
}

public class FirebasePasswordResetRequest
{
    [JsonPropertyName("requestType")]
    public string RequestType { get; set; } = "PASSWORD_RESET";

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;
}

public class FirebaseSuccessResponse
{
    [JsonPropertyName("idToken")]
    public string? IdToken { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("refreshToken")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expiresIn")]
    public string? ExpiresIn { get; set; }

    [JsonPropertyName("localId")]
    public string? LocalId { get; set; }
}

public class FirebaseErrorResponse
{
    [JsonPropertyName("error")]
    public FirebaseErrorDetail? Error { get; set; }
}

public class FirebaseErrorDetail
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public class FirebaseAuthService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public FirebaseAuthService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Firebase:ApiKey"] ?? string.Empty;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && !_apiKey.Contains("YOUR_FIREBASE_API_KEY");

    public async Task<FirebaseAuthResult> SignInWithEmailPasswordAsync(string email, string password)
    {
        if (!IsConfigured)
        {
            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = "Firebase API Key is not configured. Please add your Firebase Web API Key in appsettings.json."
            };
        }

        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={_apiKey}";
            var request = new FirebaseSignInRequest { Email = email, Password = password };
            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                var successData = await response.Content.ReadFromJsonAsync<FirebaseSuccessResponse>();
                return new FirebaseAuthResult
                {
                    Success = true,
                    IdToken = successData?.IdToken,
                    RefreshToken = successData?.RefreshToken,
                    LocalId = successData?.LocalId,
                    Email = successData?.Email
                };
            }

            var errorData = await response.Content.ReadFromJsonAsync<FirebaseErrorResponse>();
            var errorMsg = FormatFirebaseError(errorData?.Error?.Message);

            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = errorMsg
            };
        }
        catch (Exception ex)
        {
            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = $"Authentication connection error: {ex.Message}"
            };
        }
    }

    public async Task<FirebaseAuthResult> SignUpWithEmailPasswordAsync(string email, string password)
    {
        if (!IsConfigured)
        {
            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = "Firebase API Key is not configured. Please add your Firebase Web API Key in appsettings.json."
            };
        }

        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={_apiKey}";
            var request = new FirebaseSignInRequest { Email = email, Password = password };
            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                var successData = await response.Content.ReadFromJsonAsync<FirebaseSuccessResponse>();
                return new FirebaseAuthResult
                {
                    Success = true,
                    IdToken = successData?.IdToken,
                    RefreshToken = successData?.RefreshToken,
                    LocalId = successData?.LocalId,
                    Email = successData?.Email
                };
            }

            var errorData = await response.Content.ReadFromJsonAsync<FirebaseErrorResponse>();
            var errorMsg = FormatFirebaseError(errorData?.Error?.Message);

            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = errorMsg
            };
        }
        catch (Exception ex)
        {
            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = $"Registration connection error: {ex.Message}"
            };
        }
    }

    public async Task<FirebaseAuthResult> SendPasswordResetEmailAsync(string email)
    {
        if (!IsConfigured)
        {
            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = "Firebase API Key is not configured in appsettings.json."
            };
        }

        try
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={_apiKey}";
            var request = new FirebasePasswordResetRequest { Email = email };
            var response = await _httpClient.PostAsJsonAsync(url, request);

            if (response.IsSuccessStatusCode)
            {
                return new FirebaseAuthResult { Success = true };
            }

            var errorData = await response.Content.ReadFromJsonAsync<FirebaseErrorResponse>();
            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = FormatFirebaseError(errorData?.Error?.Message)
            };
        }
        catch (Exception ex)
        {
            return new FirebaseAuthResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private string FormatFirebaseError(string? firebaseErrorCode) => firebaseErrorCode switch
    {
        "EMAIL_NOT_FOUND" => "No account found with this email address.",
        "INVALID_PASSWORD" => "Incorrect password. Please try again.",
        "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password credentials.",
        "USER_DISABLED" => "This account has been disabled by an administrator.",
        "EMAIL_EXISTS" => "An account with this email address already exists.",
        "OPERATION_NOT_ALLOWED" => "Email/password sign-in is not enabled in your Firebase console.",
        "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many unsuccessful login attempts. Please try again later.",
        "WEAK_PASSWORD : Password should be at least 6 characters" => "Password must be at least 6 characters.",
        _ => !string.IsNullOrWhiteSpace(firebaseErrorCode) ? $"Authentication error: {firebaseErrorCode}" : "Authentication failed. Please check your credentials."
    };
}
