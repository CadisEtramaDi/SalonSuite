using System;
using System.Globalization;
using Google.Cloud.Firestore;

namespace SalonSuite.Services;

/// <summary>
/// Custom Firestore converter for System.Decimal since Firestore doesn't have native 128-bit decimal support.
/// Serializes decimal as 64-bit double and deserializes numbers/strings safely to decimal.
/// </summary>
public class FirestoreDecimalConverter : IFirestoreConverter<decimal>
{
    public object ToFirestore(decimal value) => Convert.ToDouble(value);

    public decimal FromFirestore(object value)
    {
        return value switch
        {
            double d => (decimal)d,
            float f => (decimal)f,
            long l => (decimal)l,
            int i => (decimal)i,
            string s when decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 0m
        };
    }
}
