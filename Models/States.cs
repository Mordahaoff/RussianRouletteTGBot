using System.Text;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using RussianRouletteTGBot.Models.Entities;
using Game = RussianRouletteTGBot.Models.Entities.Game;
using User = RussianRouletteTGBot.Models.Entities.User;
using Telegram.Bot.Types.Enums;

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
        var botMessage = "Перечень возможных команд представлен ниже.";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                // first row
                [
                    InlineKeyboardButton.WithCallbackData("Профиль 👤", "Profile"),
                    InlineKeyboardButton.WithCallbackData("Рейтинг 🏆", "Rating"),
                    InlineKeyboardButton.WithCallbackData("История 👾", "History"),
                ],
                // second row
                [
                    InlineKeyboardButton.WithCallbackData("Правила 📄", "Rules"),
                    InlineKeyboardButton.WithCallbackData("Настройки ⚙️", "Settings"),
                    InlineKeyboardButton.WithCallbackData("Бонус 🎁", "Bonus"),

                ],
                // third row
                [
                    InlineKeyboardButton.WithCallbackData("Играть 🎮", "Play"),
                ],
            ]);
        await bot.SendMessage(chatId, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
        return;
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "❔ Пожалуйста, выберите команду! ❔";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class BetState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var callbackQuery = update.CallbackQuery!;
        var userDb = await db.Users.Include(u => u.Settings).ThenInclude(u => u.TypeOfBullet).FirstAsync(u => u.TgId == callbackQuery.From!.Id, token);
        var settings = userDb.Settings.OrderBy(s => s.IdSetting).First();

        var botMessage = new StringBuilder();
        botMessage.AppendLine("Выберите ставку, на которую хотите сыграть 💸");
        botMessage.AppendLine("Минимальная сумма ставки — <b>100</b> очков ❗️");
        botMessage.AppendLine();
        botMessage.AppendLine($"Сейчас у Вас <b>{userDb.Score}</b> очков 💸");
        botMessage.AppendLine($"Вы можете поставить сейчас максимум <b>{userDb.Score - settings.TypeOfBullet.Price}</b> очков ❗️");
        botMessage.AppendLine();
        botMessage.AppendLine("<i>Настройки игры ⚙️");
        botMessage.AppendLine($"— Выбранная пуля: <b>{settings.TypeOfBullet.Title}</b>, <b>{settings.TypeOfBullet.Price}</b> очков");
        botMessage.AppendLine($"— Количество пуль: <b>{settings.CountOfBullets}</b></i>");
        // string botMessage = "BetState : Выберите ставку, на которую хотите сыграть!";

        var inlineKeyboard = new InlineKeyboardMarkup([[InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState")]]);
        await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var msg = update.Message!;
        var userTg = msg.From!;
        var chat = msg.Chat!;

        var userDb = await db.Users
            .Include(u => u.Settings)
                .ThenInclude(s => s.TypeOfBullet)
            .FirstAsync(u => u.TgId == userTg.Id, token);

        var betValue = int.TryParse(msg.Text, out int betInt);
        var bulletsType = userDb.Settings.OrderBy(s => s.IdSetting).First().TypeOfBullet;
        var diff = userDb.Score - betInt;
        string botMessage;

        // Ставка меньше 100 или ставка больше имеющегося счета --> Некорректный ввод
        if (betInt < 100 || betInt > userDb.Score)
        {
            botMessage = "❌ Пожалуйста, введите <b>корректное</b> значение ставки. ❌";
            var inlineKeyboard = new InlineKeyboardMarkup([[InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState")]]);
            await bot.SendMessage(chat.Id, botMessage, ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
            return;
        }

        // Не хватает денег на оплату пули
        if (diff < bulletsType.Price)
        {
            botMessage = "❌ <b>Упс... Не хватает денег на оплату пули.</b> ❌";
            var inlineKeyboard = new InlineKeyboardMarkup([[InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState")]]);
            await bot.SendMessage(chat.Id, botMessage, ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
            return;
        }

        botMessage = $"✅ Ставка в <b>{betInt}</b> монет принята. Начнем игру! ✅";
        userDb.Score -= betInt;
        userDb.Score -= bulletsType.Price;
        var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);
        var game = new Game()
        {
            UserId = userDb.IdUser,
            SettingsId = settings.IdSetting,
            Winning = betInt,
            Bet = betInt,
        };

        db.Users.Update(userDb);
        await db.Games.AddAsync(game, token);
        await db.SaveChangesAsync(token);

        var indexList = new List<short> { 1, 2, 3, 4, 5, 6 };
        var random = new Random();

        for (int i = 0; i < settings.CountOfBullets; i++)
        {
            var randomIndex = random.Next(0, indexList.Count);
            var bulletInGame = new BulletsInGame() { GameId = game.IdGame, IndexOfBullet = indexList[randomIndex] };
            indexList.RemoveAt(randomIndex);
            await db.BulletsInGames.AddAsync(bulletInGame, token);
        }

        await bot.SendMessage(chat.Id, botMessage, ParseMode.Html, cancellationToken: token);
        await userDb.SetStateAsync(BotState.ChoiceState, bot, db, update, token);
        await db.SaveChangesAsync(token);
    }
}

public class ChoiceState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userTgId = update.CallbackQuery != null ? update.CallbackQuery.From.Id : update.Message != null ? update.Message.From!.Id : throw new InvalidOperationException("Neither CallbackQuery nor Message is available.");
        var chatId = update.CallbackQuery != null ? update.CallbackQuery.Message!.Chat.Id : update.Message != null ? update.Message.Chat.Id : throw new InvalidOperationException("Neither CallbackQuery nor Message is available.");

        var game = await db.Games
            .Include(g => g.BulletsInGames)
            .Include(g => g.User).FirstAsync(g => g.User.TgId == userTgId && g.ResultId == null, token);

        var botMessage = new StringBuilder();
        botMessage.AppendLine($"💰 Текущий выигрыш составляет <b>{game.Winning}</b> очков.");
        botMessage.AppendLine($"🎮 Текущий раунд: <b>{game.CountOfRounds}</b>.");
        botMessage.AppendLine($"🔫 Всего пуль в барабане: <b>{game.BulletsInGames.Count}</b>.");
        botMessage.AppendLine("");
        botMessage.AppendLine("<b>Что будете делать дальше?</b>");


        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Выстрелить", "Shot"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Забрать", "Collect"),
                ]
            ]);
        await bot.SendMessage(chatId, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "❔ Пожалуйста, выберите команду! ❔";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class CollectState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userDb = await db.Users.FirstAsync(u => u.TgId == update.CallbackQuery!.From!.Id, token);
        var game = await db.Games
            .Include(g => g.BulletsInGames)
            .Include(g => g.Settings)
                .ThenInclude(g => g.TypeOfBullet)
            .FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);

        game.ResultId = (int)ResultOfGame.Collect;
        if (game.CountOfRounds != 1)
        {
            game.Winning = (int)Math.Round(game.Winning * game.Settings.TypeOfBullet.Multiplier, MidpointRounding.AwayFromZero);
        }
        userDb.Score += game.Winning;

        db.Users.Update(userDb);
        db.Games.Update(game);
        await db.SaveChangesAsync(token);

        var botMessage = new StringBuilder();
        botMessage.AppendLine("💰 <b>ПРЕЖДЕВРЕМЕННЫЙ СБОР</b> 💰");
        botMessage.AppendLine();
        botMessage.AppendLine($"💰 Вы решили забрать деньги на <b>{game.CountOfRounds}</b>-м раунде.");
        botMessage.AppendLine($"💰 Ваш выигрыш составляет <b>{game.Winning}</b> очков.");
        botMessage.AppendLine();

        botMessage.AppendLine("<b>Дополнительно 🔍</b>");
        foreach (var bullet in game.BulletsInGames.OrderBy(b => b.IndexOfBullet))
        {
            botMessage.AppendLine($"— Пуля ждала Вас на раунде <b>{bullet.IndexOfBullet}</b>.");
        }

        botMessage.AppendLine();
        botMessage.AppendLine("<b>Что будете делать дальше?</b>");

        // var botMessage = $"Collect State : Поздравляю! Вы забрали деньги!\nВаш выигрыш составляет {game.Winning} монет.";
        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
                ]
            ]);
        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "❔ Пожалуйста, выберите команду! ❔";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class WinState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userDb = await db.Users.FirstAsync(u => u.TgId == update.CallbackQuery!.From!.Id, token);
        var game = await db.Games
            .Include(g => g.Settings)
                .ThenInclude(g => g.TypeOfBullet)
            .FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);
        game.ResultId = (int)ResultOfGame.Win;

        game.Winning = (int)Math.Round(game.Winning * game.Settings.TypeOfBullet.Multiplier, MidpointRounding.AwayFromZero) + game.Bet;
        userDb.Score += game.Winning;

        db.Users.Update(userDb);
        db.Games.Update(game);
        await db.SaveChangesAsync(token);

        var botMessage = new StringBuilder();
        botMessage.AppendLine("🥇 <b>ПОЛНАЯ ПОБЕДА</b> 🥇");
        botMessage.AppendLine();
        botMessage.AppendLine("🥇 В барабане остались лишь пули, так что Вы победили.");
        botMessage.AppendLine($"🥇 Ваш выигрыш составляет <b>{game.Winning}</b> очков.");
        botMessage.AppendLine();
        botMessage.AppendLine("<b>Что будете делать дальше?</b>");

        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
                ]
            ]);
        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "❔ Пожалуйста, выберите команду! ❔";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class LoseState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userDb = await db.Users.FirstAsync(u => u.TgId == update.CallbackQuery!.From!.Id, token);
        var game = await db.Games.Include(g => g.BulletsInGames).FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);
        game.ResultId = (int)ResultOfGame.Lose;
        game.Winning = 0;

        db.Games.Update(game);
        await db.SaveChangesAsync(token);

        var botMessage = new StringBuilder();
        botMessage.AppendLine($"😓 ПРОИГРЫШ 😓");
        botMessage.AppendLine();
        botMessage.AppendLine($"😓 Вы проиграли <b>{game.Bet}</b> очков, будучи на <b>{game.CountOfRounds}</b>-м раунде.");

        if (game.BulletsInGames.Count > 1)
        {
            var list = game.BulletsInGames.OrderBy(b => b.IndexOfBullet).ToList();
            list.RemoveAt(0);

            botMessage.AppendLine();
            botMessage.AppendLine("<b>Дополнительно 🔍</b>");

            foreach (var bullet in list)
            {
                botMessage.AppendLine($"— Пуля ждала Вас на раунде <b>{bullet.IndexOfBullet}</b>.");
            }
        }

        botMessage.AppendLine();
        botMessage.AppendLine("<b>Что будете делать дальше?</b>");

        var inlineKeyboard = new InlineKeyboardMarkup(
            [
                [
                    InlineKeyboardButton.WithCallbackData("Играть дальше", "Play"),
                ],
                [
                    InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
                ]
            ]);
        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "❔ Пожалуйста, выберите команду! ❔";
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

        var botMessage = new StringBuilder();
        botMessage.AppendLine("⚙️ <b>Ваши настройки</b> ⚙️");
        botMessage.AppendLine();
        botMessage.AppendLine($"<b>Пуля 🔫</b>");
        botMessage.AppendLine($"— Пуля: <b>{settings.TypeOfBullet.Title}</b>");
        botMessage.AppendLine($"— Множитель пули: <b>{settings.TypeOfBullet.Multiplier}</b>");
        botMessage.AppendLine($"— Стоимость пули: <b>{settings.TypeOfBullet.Price}</b>");
        botMessage.AppendLine();
        botMessage.AppendLine($"<b>Количество пуль 🔫</b>");
        botMessage.AppendLine($"— Количество пуль при создании игры: <b>{settings.CountOfBullets}</b>.");
        botMessage.AppendLine();
        botMessage.AppendLine("<i><b>📄 Пояснение 📄</b>");
        botMessage.AppendLine("В случае победы или преждевременного сбора выигрыша суммарный выигрыш увеличивается во <b>множитель пули</b> раз.");
        botMessage.AppendLine("В случае прохождения в следующий раунд суммарный выигрыш увеличивается во <b>множитель количества пуль</b> раз.");
        botMessage.AppendLine("Подробная информация касательно пуль и их количества представлены в разделе по их изменении.</i>");


        var inlineKeyboard = new InlineKeyboardMarkup([
            [
                InlineKeyboardButton.WithCallbackData("Тип пули", "SetBulletsType"),
                InlineKeyboardButton.WithCallbackData("Кол-во пуль", "SetBulletsCount"),
            ],
            [
                InlineKeyboardButton.WithCallbackData("Вернуться", "ToWaitingState"),
            ]
        ]);

        await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
        return;
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "❔ Пожалуйста, выберите команду!❔ ";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class SetBulletsTypeState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var bulletsTypeList = await db.TypesOfBullets.ToListAsync(token);

        var botMessage = new StringBuilder();
        botMessage.AppendLine("🔫 <b>Изменение типа пули</b> 🔫");
        botMessage.AppendLine();
        botMessage.AppendLine("Выберите один из предложенных ниже вариантов.");
        botMessage.AppendLine("Ниже представлены <b>название</b>, <b>множитель</b> и <b>цена</b> каждой пули соответственно.");
        botMessage.AppendLine();

        for (int i = 0; i < bulletsTypeList.Count; i++)
        {
            var type = bulletsTypeList[i];
            botMessage.AppendLine($"{i + 1}. <b>{type.Title} пуля</b> | <b>{type.Multiplier}</b>x | <b>{type.Price}</b> очков");
        }

        botMessage.AppendLine();
        botMessage.AppendLine("<i><b>📄 Пояснение 📄</b>");
        botMessage.AppendLine("Тип пули увеличивает конечный выигрыш в значение, равному множителю этой пули. <b>При создании новой игры количество Ваших очков будет уменьшаться согласно стоимости пули.</b></i>");

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

        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "❔ Пожалуйста, выберите команду! ❔";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class SetBulletsCountState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = new StringBuilder();
        botMessage.AppendLine("🔫 <b>Изменение количества пуль</b> 🔫");
        botMessage.AppendLine();
        botMessage.AppendLine("Выберите один из предложенных ниже вариантов.");
        botMessage.AppendLine("Ниже представлены <b>количество пуль</b> в барабане и <b>множитель</b> за раунд.");
        botMessage.AppendLine();

        for (int i = 1; i <= 6; i++)
        {
            botMessage.AppendLine($"{i}. Кол-во: <b>{i}</b>, множитель: <b>{MultiplierFactory.GetMultiplier(i)}</b>x");
        }

        botMessage.AppendLine();
        botMessage.AppendLine("<i><b>📄 Пояснение 📄</b>");
        botMessage.AppendLine("Количество пуль в барабане увеличивает выигрыш с каждым прожитым раундом в значение, равное множителю этого количества.</i>");

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

        await bot.SendMessage(update.CallbackQuery!.Message!.Chat.Id, botMessage.ToString(), ParseMode.Html, replyMarkup: inlineKeyboard, cancellationToken: token);
        return;
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "Пожалуйста, выберите команду!";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}

public class WinOrChoiceState : State
{
    public override async Task EnterAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var userDb = await db.Users.FirstAsync(u => u.TgId == update.CallbackQuery!.From!.Id, token);
        var game = await db.Games.Include(g => g.BulletsInGames).FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);

        // Если остались последние раунды, где только пули, то победа
        if (6 - game.CountOfRounds == game.BulletsInGames.Count)
        {
            await userDb.SetStateAsync(BotState.WinState, bot, db, update, token);
        }
        else
        {
            game.CountOfRounds++;
            db.Games.Update(game);
            await db.SaveChangesAsync(token);
            await userDb.SetStateAsync(BotState.ChoiceState, bot, db, update, token);
        }
    }

    public override async Task DoAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
    {
        var botMessage = "⏳ Пожалуйста, подождите! Производится расчет выстрела. ⏳";
        await bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
    }
}