using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models.Entities;

public partial class BulletsInGame
{
    public int IdBulletInGame { get; set; }

    public int GameId { get; set; }

    public short IndexOfBullet { get; set; }

    public virtual Game Game { get; set; } = null!;
}
