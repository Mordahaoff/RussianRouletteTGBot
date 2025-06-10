using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models;

public partial class User
{
    public int IdUser { get; set; }

    public long TgId { get; set; }

    public int BotStateId { get; set; }

    public int MaxScore { get; set; }

    public int Score { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();

    public virtual ICollection<MoneyBonuse> MoneyBonuses { get; set; } = new List<MoneyBonuse>();

    public virtual ICollection<Setting> Settings { get; set; } = new List<Setting>();

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
