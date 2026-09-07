using System;

namespace SalonSuite.Models;

public class TestimonialItem
{
    public int Id { get; set; }
    public string Quote { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int Rating { get; set; } = 5;
}
