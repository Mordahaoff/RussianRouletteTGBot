using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models;

public partial class Setting
{
    public int IdSetting { get; set; }

    public int UserId { get; set; }

    public int TypeOfBulletId { get; set; }

    public short CountOfBullets { get; set; }

    public virtual ICollection<Game> Games { get; set; } = new List<Game>();

    public virtual TypesOfBullet TypeOfBullet { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
