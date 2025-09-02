using Microsoft.EntityFrameworkCore;
using RussianRouletteTGBot.Models.Entities;

namespace RussianRouletteTGBot.Models.Extensions;

public partial class InfoMessage
{
    public static async Task<Entities.InfoMessage> GetInfoMessageAsyncByTgId(RouletteContext db, long userTgId, CancellationToken token)
    {
        var infoMessage = await db.InfoMessages
            .Include(im => im.User)
            .FirstOrDefaultAsync(im => im.User.TgId == userTgId, token)
            ?? throw new NullReferenceException($"InfoMessage for user TG id: {userTgId} not found.");
        return infoMessage;
    }

    public static async Task<Entities.InfoMessage> GetInfoMessageAsyncByDbId(RouletteContext db, long userDbId, CancellationToken token)
    {
        var infoMessage = await db.InfoMessages
            .FirstOrDefaultAsync(im => im.UserId == userDbId, token)
            ?? throw new NullReferenceException($"InfoMessage for user DB id: {userDbId} not found.");
        return infoMessage;
    }
}