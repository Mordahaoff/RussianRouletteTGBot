using System.Text;

namespace RussianRouletteTGBot.Models;

public static class TxtToStringBuilder
{
    public static async Task<StringBuilder> FromTxtToStringBuilder(string path, CancellationToken token)
    {
        var lines = await File.ReadAllLinesAsync("files/txt/Start.txt", Encoding.UTF8, token);
        var stringBuilder = new StringBuilder();

        foreach (var line in lines)
        {
            stringBuilder.AppendLine(line);
        }

        return stringBuilder;
    }
}