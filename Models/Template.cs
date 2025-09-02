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
        _template = await File.ReadAllTextAsync(path, _token);
        if (string.IsNullOrEmpty(_template)) throw new NullReferenceException($"The text read from path: {path} is null.");
    }



    public void Format(Dictionary<string, string> dict)
    {
        foreach (var item in dict)
        {
            _template = _template.Replace(item.Key, item.Value);
        }
    }

    public string GetTemplate() => _template;
}