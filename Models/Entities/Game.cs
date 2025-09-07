namespace RussianRouletteTGBot.Models.Entities;

public partial class Game
{
    public int IdGame { get; set; }

    public int UserId { get; set; }

    public int SettingsId { get; set; }

    public int? ResultId { get; set; }

    public short CountOfRounds { get; set; }

    public int Winning { get; set; }

    public int Bet { get; set; }

    public virtual ICollection<BulletsInGame> BulletsInGames { get; set; } = new List<BulletsInGame>();

    public virtual ResultsOfGame? Result { get; set; }

    public virtual Setting Settings { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
