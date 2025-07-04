using RussianRouletteTGBot.Models;

internal class Program
{
    private static void Main(string[] args)
    {
        var host = new Host("Access_Token", "Host=localhost;Port=5432;Database=roulette;Username=admin;Password=admin");
        host.Start();
        Console.ReadLine();
    }
}
