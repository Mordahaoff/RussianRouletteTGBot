using System.Runtime.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace RussianRouletteTGBot.Models;

public partial class User
{
    public async Task SetStateAsync(BotState newState, ITelegramBotClient client, RouletteContext db, Update update, CancellationToken token)
    {
        this.BotStateId = (int)newState;
        db.Users.Update(this);

        var stateInstance = StateFactory.GetState(newState);
        await stateInstance.EnterAsync(client, db, update, token);

        await db.SaveChangesAsync(token);
    }

    public async Task DoStateAsync(ITelegramBotClient client, RouletteContext db, Update update, CancellationToken token)
    {
        if (Enum.TryParse<BotState>(BotStateId.ToString(), out var botstate))
        {
            var state = StateFactory.GetState(botstate);
            await state.DoAsync(client, db, update, token);
        }
        throw new ArgumentException($"Unknown BotStateId: {BotStateId}");
    }
}