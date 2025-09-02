namespace RussianRouletteTGBot.Models;

public class Options
{
    public ConnectionStrings ConnectionStrings { get; set; } = null!;
    public string TokenAPI { get; set; } = null!;
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = null!;
}
