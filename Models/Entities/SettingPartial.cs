using Microsoft.EntityFrameworkCore;

namespace RussianRouletteTGBot.Models.Entities;

public partial class Setting
{
    public static async Task<Setting> GetSettingsAsync(RouletteContext db, long userTgId, CancellationToken token)
    {
        var settings = await db.Settings
            .Include(s => s.TypeOfBullet)
            .Include(s => s.User)
            .FirstAsync(s => s.User.TgId == userTgId, token)
            ?? throw new NullReferenceException($"Settings for user id: {userTgId} not found.");
        return settings;
    }
}