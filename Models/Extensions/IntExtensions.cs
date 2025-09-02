namespace RussianRouletteTGBot.Models.Extensions;

public static class IntExtensions
{
    public static string ToNumberWithDots(this int value)
    {
        string result = value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        result = result.Replace(",", ".");
        return result;
    }
}