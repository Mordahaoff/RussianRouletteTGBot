using System.Text.Json;
using RussianRouletteTGBot.Models;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Options? options;

        var fileName = "appsettings.json"; // Имя файла
        string basePath = AppContext.BaseDirectory; // Получаем путь к папке, из которой запущено приложение
        string projectRoot = Path.GetFullPath(Path.Combine(basePath, "../../..")); // Чтобы подняться на уровень выше и попасть в корень проекта
        string filePath = Path.Combine(projectRoot, fileName); // Полный путь к файлу

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            options = JsonSerializer.Deserialize<Options>(json);
            if (options == null || options.TokenAPI == null || options.ConnectionStrings.DefaultConnection == null)
            {
                Console.Error.WriteLine("Не удалось десериализировать настройки из appsettings.json.");
                return;
            }
        }
        catch (FileNotFoundException)
        {
            Console.Error.WriteLine($"Файл {filePath} не найден.");
            return;
        }
        catch (JsonException)
        {
            Console.Error.WriteLine($"Ошибка при парсинге файла {filePath}");
            return;
        }

        var host = new Host(options.TokenAPI, options.ConnectionStrings.DefaultConnection);
        host.Start();
        Console.ReadLine();
    }
}
