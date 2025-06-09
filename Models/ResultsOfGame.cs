using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models;

public partial class ResultsOfGame
{
    public int IdResultOfGame { get; set; }

    public string Title { get; set; } = null!;

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
