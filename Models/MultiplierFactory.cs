namespace RussianRouletteTGBot.Models;

public static class MultiplierFactory
{
    private static readonly Dictionary<int, double> _multiplierDict = new()
    {
        { 1, 1.3 },
        { 2, 1.5 },
        { 3, 1.8 },
        { 4, 2.75 },
        { 5, 8 },
        { 6, 30 },
    };

    public static double GetMultiplier(int countOfBullets)
    {
        if (_multiplierDict.TryGetValue(countOfBullets, out var multiplier))
            return multiplier;
        throw new ArgumentException($"Unknown countOfBullets: {countOfBullets}");
    }
}