namespace RussianRouletteTGBot.Models.Entities;

public partial class MoneyBonuse
{
    public int IdMoneyBonus { get; set; }

    public int UserId { get; set; }

    public DateTime CollectionTime { get; set; }

    public virtual User User { get; set; } = null!;
}
