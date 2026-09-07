using System;

namespace SalonSuite.Models;

public class UserSession
{
    public bool IsLoggedIn { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
