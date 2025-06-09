using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models;

public partial class TypesOfBullet
{
    public int IdTypeOfBullet { get; set; }

    public string Title { get; set; } = null!;

    public decimal Multiplier { get; set; }

    public short Price { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
