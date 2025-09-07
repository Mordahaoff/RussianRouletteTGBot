namespace RussianRouletteTGBot.Models.Entities;

public partial class TypesOfBullet
{
    public int IdTypeOfBullet { get; set; }

    public string Title { get; set; } = null!;

    public decimal Multiplier { get; set; }

    public short Price { get; set; }

    public virtual ICollection<Setting> Settings { get; set; } = new List<Setting>();
}
