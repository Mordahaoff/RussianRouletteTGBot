using Microsoft.EntityFrameworkCore;

namespace RussianRouletteTGBot.Models.Entities;

public partial class Game
{
    public static async Task<Game> GetGameWithBulletsByUserTgId(RouletteContext db, long userTgId, CancellationToken token)
    {
        var game = await db.Games
            .Include(g => g.User)
            .Include(g => g.BulletsInGames)
            .OrderByDescending(g => g.IdGame)
            .FirstAsync(g => g.User.TgId == userTgId, token);
        return game;
    }
}