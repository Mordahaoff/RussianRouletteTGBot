using System.Security.Principal;
using System.Text;

namespace RussianRouletteTGBot.Models;

public class Template(CancellationToken token)
{
    private string _template = "";
    private readonly CancellationToken _token = token;

    public async Task ReadTemplateAsync(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new ArgumentNullException($"Path: {path} is null.");
        var fullPath = GetFullPath(path);
        if (string.IsNullOrEmpty(fullPath)) throw new ArgumentNullException($"Fullpath: {fullPath} is null.");
        _template = await File.ReadAllTextAsync(fullPath, _token);
        if (string.IsNullOrEmpty(_template)) throw new NullReferenceException($"The text read from path: {fullPath} is null.");
    }

    public void Format(Dictionary<string, string> dict)
    {
        foreach (var item in dict)
        {
            _template = _template.Replace(item.Key, item.Value);
        }
    }

    public string GetTemplate() => _template;

    public static string GetFullPath(string path)
    {
        string basePath = AppContext.BaseDirectory; // Получаем путь к папке, из которой запущено приложение
        string projectRoot = Path.GetFullPath(Path.Combine(basePath, "../../..")); // Чтобы подняться на уровень выше и попасть в корень проекта
        string filePath = Path.Combine(projectRoot, path); // Полный путь к файлу
        return filePath;
    }
}