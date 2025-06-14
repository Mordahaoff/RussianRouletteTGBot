using Telegram.Bot;
using Telegram.Bot.Types;

namespace RussianRouletteTGBot.Models.Entities;

public partial class User
{
    public async Task SetStateAsync(BotState newState, ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        this.BotStateId = (int)newState;
        db.Users.Update(this);

        var stateInstance = StateFactory.GetState(newState);
        await stateInstance.EnterAsync(bot, db, update, token);

        // await db.SaveChangesAsync(token);
    }

    public async Task DoStateAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        if (Enum.TryParse<BotState>(this.BotStateId.ToString(), out var botstate))
        {
            var stateInstance = StateFactory.GetState(botstate);
            await stateInstance.DoAsync(bot, db, update, token);
        }
        else
        {
            throw new ArgumentException($"Unknown BotStateId: {this.BotStateId}");
        }
    }
}