using System.Text;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using RussianRouletteTGBot.Models.Entities;
using Game = RussianRouletteTGBot.Models.Entities.Game;

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
        long chatId = update.CallbackQuery != null
            ? update.CallbackQuery.Message!.Chat.Id
            : update.Message != null
                ? update.Message.Chat.Id
                : throw new InvalidOperationException("Neither CallbackQuery nor Message is available.");
        var botMessage = "WaitingState : Перечень возможных команд.";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                // first row
                [
                    InlineKeyboardButton.WithCallbackData("Профиль", "Profile"),
                    InlineKeyboardButton.WithCallbackData("Достижения", "Achievements"),
                    InlineKeyboardButton.WithCallbackData("История", "History"),
                ],
                // second row
                [
                    InlineKeyboardButton.WithCallbackData("Правила", "Rules"),
                    InlineKeyboardButton.WithCallbackData("Настройки", "Settings"),
                    InlineKeyboardButton.WithCallbackData("Бонус", "Bonus"),

                ],
                // third row
                [
                    InlineKeyboardButton.WithCallbackData("Играть", "Play"),
                ],
            ]);
        await bot.SendMessage(chatId, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
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
        var callbackQuery = update.CallbackQuery!;
        var userDb = await db.Users.FirstAsync(u => u.TgId == callbackQuery.From!.Id, token);

        string botMessage = "BetState : Выберите ставку, на которую хотите сыграть!";
        var inlineKeyboard = new InlineKeyboardMarkup([[InlineKeyboardButton.WithCallbackData("Вернутся", "ToWaitingState")]]);
        await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
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
            userDb.Score -= betInt;
            var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);
            var game = new Game()
            {
                UserId = userDb.IdUser,
                SettingsId = settings.IdSetting,
                Bet = betInt,
            };
            db.Users.Update(userDb);
            await db.Games.AddAsync(game, token);
            await db.SaveChangesAsync(token);

            var indexList = new List<int> { 1, 2, 3, 4, 5, 6 };
            var random = new Random();

            var logMessage = $"\nПользователь ({userTg.FirstName}) [ID:{userTg.Id}] создал новую игру.";
            for (int i = 0; i < settings.CountOfBullets; i++)
            {
                var randomIndex = random.Next(0, indexList.Count);
                indexList.RemoveAt(randomIndex);
                var bulletInGame = new BulletsInGame() { GameId = game.IdGame, IndexOfBullet = (short)(randomIndex + 1) };
                await db.BulletsInGames.AddAsync(bulletInGame, token);
                logMessage += $"IndexOfBullet: {bulletInGame.IndexOfBullet}\n";
            }

            await userDb.SetStateAsync(BotState.ChoiceState, bot, db, update, token);
            db.Users.Update(userDb);

            await db.SaveChangesAsync(token);

            Console.WriteLine(logMessage);
        }
        await bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
    }
}

public class ChoiceState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userTgId = update.CallbackQuery!.From.Id;
        var game = await db.Games.Include(g => g.User).FirstAsync(g => g.User.TgId == userTgId, token);
        var botMessage = $"ChoiceState : Ваш выбор:\nТекущий раунд: {game.CountOfRounds}";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Выстрелить", "Shot"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Забрать", "Collect"),
                ]
            ]);
        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "ChoiceState : Пожалуйста, выберите действие!";
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

        db.Users.Update(userDb);
        db.Games.Update(game);
        await db.SaveChangesAsync(token);

        var botMessage = $"Collect State : Поздравляю! Вы забрали деньги!\nВаш выигрыш составляет {winValue} монет.";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
                ]
            ]);
        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "Collect State : Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class WinState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userDb = await db.Users.FirstAsync(u => u.TgId == update.Message!.From!.Id, token);
        var game = await db.Games
            .Include(g => g.Settings)
                .ThenInclude(g => g.TypeOfBullet)
            .FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);
        game.ResultId = (int)ResultOfGame.Win;

        var winValue = 200; // ПЕРЕСЧИТАТЬ ФОРМУЛУ РАСЧЕТА ВЫИГРЫША
        userDb.Score += winValue;
        game.Winning = winValue;

        db.Users.Update(userDb);
        db.Games.Update(game);
        await db.SaveChangesAsync(token);

        var botMessage = $"WinState : Поздравляю! Вы победили!\nВаш выигрыш составляет {winValue} монет.";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
                ]
            ]);
        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "WinState : Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class LoseState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userDb = await db.Users.FirstAsync(u => u.TgId == update.Message!.From!.Id, token);
        var game = await db.Games.FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);
        game.ResultId = (int)ResultOfGame.Lose;

        db.Games.Update(game);
        await db.SaveChangesAsync(token);

        var botMessage = $"LoseState : Увы, вы проиграли!\nВы потеряли {game.Bet} монет.";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
                ]
            ]);
        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "LoseState : Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class SettingsState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var callbackQuery = update.CallbackQuery!;
        var userDb = await db.Users.FirstAsync(u => u.TgId == callbackQuery.From.Id, token);

        var settings = await db.Settings
            .Include(s => s.TypeOfBullet)
            .FirstAsync(s => s.UserId == userDb.IdUser, token);

        var botMessage = $"Ваши настройки:" +
            $"Пуля: {settings.TypeOfBullet.Title} | {settings.TypeOfBullet.Multiplier} | {settings.TypeOfBullet.Price}" +
            $"Кол-во: {settings.CountOfBullets}";

        var inlineKeyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("Тип пули", "SetBulletsType"),
                InlineKeyboardButton.WithCallbackData("Кол-во пуль", "SetBulletsCount"),
            ],
            [
                InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
            ]
        ]);

        await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
        return;
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "SettingsState : Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class SetBulletsTypeState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var bulletsTypeList = await db.TypesOfBullets.ToListAsync(token);

        var botMessage = new StringBuilder($"Выберите один из доступных типов пуль:");
        foreach (var type in bulletsTypeList)
        {
            botMessage.AppendLine($"{type.Title} | {type.Multiplier} | {type.Price}");
        }

        var inlineKeyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("Обычная", "SetBulletsTypeTo_Common"),
                InlineKeyboardButton.WithCallbackData("Медная", "SetBulletsTypeTo_Copper"),
                InlineKeyboardButton.WithCallbackData("Серебряная", "SetBulletsTypeTo_Silver"),
            ],
            [
                InlineKeyboardButton.WithCallbackData("Золотая", "SetBulletsTypeTo_Golden"),
                InlineKeyboardButton.WithCallbackData("Платиновая", "SetBulletsTypeTo_Platinum"),
            ],
            [
                InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState")
            ]
        ]);

        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage.ToString(), replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "SetBulletsTypeState : Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class SetBulletsCountState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "Выберите число от 1 до 6";

        var inlineKeyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("1", "SetBulletsCountTo_1"),
                InlineKeyboardButton.WithCallbackData("2", "SetBulletsCountTo_2"),
                InlineKeyboardButton.WithCallbackData("3", "SetBulletsCountTo_3"),
            ],
            [
                InlineKeyboardButton.WithCallbackData("4", "SetBulletsCountTo_4"),
                InlineKeyboardButton.WithCallbackData("5", "SetBulletsCountTo_5"),
                InlineKeyboardButton.WithCallbackData("6", "SetBulletsCountTo_6"),
            ],
            [
                InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState")
            ]
        ]);

        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
        return;
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "SetBulletsCountState : Пожалуйста, выберите действие!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}