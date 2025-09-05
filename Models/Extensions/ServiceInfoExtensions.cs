using Microsoft.EntityFrameworkCore;
using RussianRouletteTGBot.Models.Entities;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace RussianRouletteTGBot.Models.Extensions;

public partial class ServiceInfo
{
    public static async Task<Entities.ServiceInfo> GetServiceInfoAsyncByTgId(RouletteContext db, long userTgId, CancellationToken token)
    {
        var ServiceInfo = await db.ServiceInfos
            .Include(si => si.User)
            .FirstOrDefaultAsync(si => si.User.TgId == userTgId, token)
            ?? throw new NullReferenceException($"ServiceInfo for user TG id: {userTgId} not found.");
        return ServiceInfo;
    }

    public static async Task<Entities.ServiceInfo> GetServiceInfoAsyncByDbId(RouletteContext db, long userDbId, CancellationToken token)
    {
        var ServiceInfo = await db.ServiceInfos
            .FirstOrDefaultAsync(si => si.UserId == userDbId, token)
            ?? throw new NullReferenceException($"ServiceInfo for user DB id: {userDbId} not found.");
        return ServiceInfo;
    }

    public static async Task SendAndUpdateServiceInfoInDbAsync(ITelegramBotClient bot, RouletteContext db, Entities.ServiceInfo si, long chatId, string botMessage, InlineKeyboardMarkup inlineKeyboard, CancellationToken token)
    {
        var sentMessage = await bot.SendMessage(chatId, botMessage, Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
        si.IdMessage = sentMessage.Id;
        db.ServiceInfos.Update(si);
    }
}