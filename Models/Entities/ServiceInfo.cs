using System;
using System.Collections.Generic;

namespace RussianRouletteTGBot.Models.Entities;

public partial class ServiceInfo
{
    public int IdServiceInfo { get; set; }

    public int UserId { get; set; }

    public long? IdMessage { get; set; }

    public virtual User User { get; set; } = null!;
}
