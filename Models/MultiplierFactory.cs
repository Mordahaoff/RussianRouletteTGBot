namespace RussianRouletteTGBot.Models;

public static class MultiplierFactory
{
    public const double DEFAULT_MULTIPLIER = 1.3;

    private static readonly Dictionary<int, double> _multiplierDict = new()
    {
        { 1, 1.3 },
        { 2, 1.5 },
        { 3, 1.8 },
        { 4, 2.75 },
        { 5, 8 },
        { 6, 30 },
    };

    public static double GetMultiplier(int countOfBullets) => _multiplierDict[countOfBullets];
}