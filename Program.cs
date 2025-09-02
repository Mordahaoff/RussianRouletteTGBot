using System.Text.Json;
using RussianRouletteTGBot.Models;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Options? options;

        try
        {
            var json = await File.ReadAllTextAsync("appsettings.json");
            options = JsonSerializer.Deserialize<Options>(json);
            if (options == null || options.TokenAPI == null || options.ConnectionStrings.DefaultConnection == null)
            {
                Console.Error.WriteLine("Не удалось десериализировать настройки из appsettings.json.");
                return;
            }
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine("Файл appsettings.json не найден.");
            return;
        }
        catch (JsonException)
        {
            Console.Error.WriteLine("Ошибка при парсинге файла appsettings.json");
            return;
        }

        var host = new Host(options.TokenAPI, options.ConnectionStrings.DefaultConnection);
        host.Start();

        Console.WriteLine("Программа запущена...");
        Console.ReadLine();
    }
}
