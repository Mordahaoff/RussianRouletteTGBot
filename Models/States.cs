using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace RussianRouletteTGBot.Models;

public abstract class State
{
    public abstract Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token);

    public abstract Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token);
}

public class WaitingState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "WaitingState : Перечень возможных команд.";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                // first row
                [
                    InlineKeyboardButton.WithCallbackData("Профиль", "Profile"),
                    InlineKeyboardButton.WithCallbackData("Достижения", "Achievements"),
                ],
                // second row
                [
                    InlineKeyboardButton.WithCallbackData("Бонус", "Bonus"),
                    InlineKeyboardButton.WithCallbackData("История", "History"),

                ],
                [
                    InlineKeyboardButton.WithCallbackData("Правила", "Rules"),
                    InlineKeyboardButton.WithCallbackData("Настройки", "Settings"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Играть", "Play"),
                ],
            ]);
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
        return;
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "WaitingState : Пожалуйста, выберите команду!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class BetState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var msg = update.Message!;
        var userDb = await db.Users.FirstAsync(u => u.TgId == msg.From!.Id, token);

        string botMessage = "BetState : Выберите ставку, на которую хотите сыграть!";
        await bot.SendMessage(msg.Chat.Id, botMessage, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var msg = update.Message!;
        var userTg = msg.From!;
        var chat = msg.Chat!;

        var userDb = await db.Users.FirstAsync(u => u.TgId == userTg.Id, token);

        var betValue = int.TryParse(msg.Text, out int betInt);
        string botMessage;
        if (betInt <= 0 || betInt > userDb.Score)
        {
            botMessage = "BetState : Пожалуйста, введите корректное значение ставки.";
        }
        else
        {
            botMessage = $"BetState : Ставка в {betInt} монет принята. Начнем игру!";
            var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);
            var game = new Game()
            {
                UserId = userDb.IdUser,
                SettingsId = settings.IdSetting,
                Bet = betInt,
            };
            await db.Games.AddAsync(game, token);
            await db.SaveChangesAsync(token);

            var indexList = new List<int> { 1, 2, 3, 4, 5, 6 };
            var random = new Random();

            for (int i = 0; i < settings.CountOfBullets; i++)
            {
                var randomIndex = random.Next(0, indexList.Count);
                var bulletIndex = indexList[randomIndex];
                indexList.RemoveAt(randomIndex);
                var bulletInGame = new BulletsInGame() { GameId = game.IdGame, IndexOfBullet = (short)bulletIndex };
                await db.BulletsInGames.AddAsync(bulletInGame, token);
            }

            userDb.BotStateId = (int)BotState.ChoiceState;
            db.Users.Update(userDb);

            await db.SaveChangesAsync(token);
        }
        await bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
    }
}

public class ChoiceState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "Ваш выбор:";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Выстрелить", "Shot"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Забрать", "Collect"),
                ]
            ]);
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class CollectState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userDb = await db.Users.FirstAsync(u => u.TgId == update.Message!.From!.Id, token);
        var game = await db.Games
            .Include(g => g.Settings)
                .ThenInclude(g => g.TypeOfBullet)
            .FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);
        game.ResultId = (int)ResultOfGame.Collect;

        var winValue = 100; // ПЕРЕСЧИТАТЬ ФОРМУЛУ РАСЧЕТА ВЫИГРЫША
        userDb.Score += winValue;
        game.Winning = winValue;

        await db.SaveChangesAsync(token);

        var botMessage = "Поздравляю! Вы забрали деньги!\nВаш выигрыш составляет";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Вернуться", "Collect"),
                ]
            ]);
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class WinState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        // var userDb = await db.Users.FirstAsync(u => u.TgId == update.Message!.From!.Id, token);
        // var game = await db.Games
        //     .Include(g => g.Settings)
        //         .ThenInclude(g => g.TypeOfBullet)
        //     .FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);
        // game.ResultId = (int)ResultOfGame.Collect;

        // var winValue = 100; // ПЕРЕСЧИТАТЬ ФОРМУЛУ РАСЧЕТА ВЫИГРЫША
        // userDb.Score += winValue;

        // var botMessage = "Поздравляю! Вы забрали деньги!\nВаш выигрыш составляет";
        // var inlineKeyboard = new InlineKeyboardMarkup(
        //     [
        //         [
        //             InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
        //         ],
        //         [
        //             InlineKeyboardButton.WithCallbackData("Вернуться", "Collect"),
        //         ]
        //     ]);
        // await bot.SendMessage(update.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        // var botMessage = "Пожалуйста, выберите действие!";
        // await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}