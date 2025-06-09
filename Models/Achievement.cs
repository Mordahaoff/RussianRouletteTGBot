using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models;

public partial class Achievement
{
    public int IdAchivevement { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;
}
