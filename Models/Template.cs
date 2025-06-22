using System.Security.Principal;
using System.Text;

namespace RussianRouletteTGBot.Models;

public class Template(CancellationToken token)
{
    private string _template = "";
    private CancellationToken _token = token;

    public async Task ReadTemplateAsync(string path) => _template = await File.ReadAllTextAsync(path, _token);

    public void Format(Dictionary<string, string> dict)
    {
        foreach (var item in dict)
        {
            _template = _template.Replace(item.Key, item.Value);
        }
    }

    public string GetTemplate() => _template;
}

/* 
var dict = new Dictionary<string, string> {
    { "{score}", userDb.Score.ToString() },
    { "{maxBet}", (userDb.Score - settings.TypeOfBullet.Price).ToString() },
    { "{bulletsTitle}", settings.TypeOfBullet.Title },
    { "{bulletsPrice}", settings.TypeOfBullet.Price.ToString() },
    { "{count}", settings.CountOfBullets.ToString() },
};

var template = new Template(token);
await template.ReadTemplateAsync("files/txt/BetState.txt");
template.Format(dict);
var botMessage = template.GetTemplate();
*/