using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models.Entities;

public partial class UserAchievement
{
    public int IdUserAchievement { get; set; }

    public int UserId { get; set; }

    public int AchievementId { get; set; }

    public DateOnly DateReceived { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
