using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models.Entities;

public partial class User
{
    public int IdUser { get; set; }

    public string FirstName { get; set; } = null!;

    public long TgId { get; set; }

    public int BotStateId { get; set; }

    public int MaxScore { get; set; }

    public int Score { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();

    public virtual InfoMessage? InfoMessage { get; set; }

    public virtual MoneyBonuse? MoneyBonuse { get; set; }

    public virtual Setting? Setting { get; set; }
}
