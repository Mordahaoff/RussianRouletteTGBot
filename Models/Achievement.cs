using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models;

public partial class Achievement
{
    public int IdAchievement { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
