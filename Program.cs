internal class Program
{
    private static void Main(string[] args)
    {
        Host host = new Host("6610715284:AAHIBRkS9W-gOeby89ZRpHH5QgAGMeGlZtU", "Host=localhost;Port=5432;Database=roulette;Username=admin;Password=admin");
        host.Start();
        Console.ReadLine();
    }
}