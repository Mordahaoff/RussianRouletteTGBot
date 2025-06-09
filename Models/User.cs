using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models;

public partial class User
{
    public int IdUser { get; set; }

    public long TgId { get; set; }

    public int BotStateId { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();
}
