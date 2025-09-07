namespace RussianRouletteTGBot.Models.Entities;

public partial class ResultsOfGame
{
    public int IdResultOfGame { get; set; }

    public string Title { get; set; } = null!;

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
