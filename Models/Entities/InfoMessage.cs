using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models.Entities;

public partial class InfoMessage
{
    public int IdInfoMessage { get; set; }

    public int UserId { get; set; }

    public int? IdWaiting { get; set; }

    public int? IdChoice { get; set; }

    public virtual User User { get; set; } = null!;
}
