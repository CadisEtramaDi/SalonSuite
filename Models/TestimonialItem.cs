using System;
using Google.Cloud.Firestore;

namespace SalonSuite.Models;

[FirestoreData]
public class TestimonialItem
{
    [FirestoreProperty]
    public int Id { get; set; }

    [FirestoreProperty]
    public string Quote { get; set; } = string.Empty;

    [FirestoreProperty]
    public string Author { get; set; } = string.Empty;

    [FirestoreProperty]
    public int Rating { get; set; } = 5;
}
