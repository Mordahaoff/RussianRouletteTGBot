using Microsoft.EntityFrameworkCore;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using RussianRouletteTGBot.Models.Entities;
using User = RussianRouletteTGBot.Models.Entities.User;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Telegram.Bot.Types.ReplyMarkups;

namespace RussianRouletteTGBot.Models;

public class Host
{
	private readonly ITelegramBotClient _bot;
	private readonly ReceiverOptions _receiverOptions;
	private Action<ITelegramBotClient, Update>? _onMessage;
	private Action<ITelegramBotClient, Update>? _onCallbackQuery;
	private readonly RouletteContext _db;

	public Host(string token, string connectionString)
	{
		_bot = new TelegramBotClient(token);
		_receiverOptions = new ReceiverOptions()
		{
			AllowedUpdates = [
				UpdateType.Message,
				UpdateType.CallbackQuery,
			],
			DropPendingUpdates = true,
		};

		// Создаём билдер настроек
		var optionsBuilder = new DbContextOptionsBuilder<RouletteContext>();
		optionsBuilder.UseNpgsql(connectionString); // вызываем расширение на билдере

		// Передаём полученные опции в конструктор контекста
		_db = new RouletteContext(optionsBuilder.Options);
	}

	public void Start()
	{
		var cts = new CancellationTokenSource();
		_bot.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);
		_onMessage = AnyMessage;
		_onCallbackQuery = AnyCallbackQuery;
		var me = _bot.GetMe().Result;
		Console.WriteLine($"Бот запущен: [{me.FirstName}] ({me.Id})");
	}

	private async Task ErrorHandler(ITelegramBotClient client, Exception exception, HandleErrorSource source, CancellationToken token)
	{
		var msg = exception switch
		{
			ApiRequestException apiRequestException => $"Telegram API Error:{apiRequestException.Message}, {apiRequestException.Source}",
			_ => $"Error: [{exception}]"
		};
		Console.WriteLine(msg);
		await Task.CompletedTask;
	}

	private async Task UpdateHandler(ITelegramBotClient client, Update update, CancellationToken token)
	{
		switch (update.Type)
		{
			case UpdateType.Message:
				{
					_onMessage?.Invoke(client, update);
					var msg = update.Message!;
					var userTg = msg.From!;
					var chat = msg.Chat!;

					if (!await _db.Users.AnyAsync(u => u.TgId == userTg.Id, token) && msg.Text == "/start")
					{
						// Добавление нового юзера
						var userDb = new User() { TgId = userTg.Id, Score = 500, MaxScore = 500, FirstName = userTg.FirstName };
						await _db.Users.AddAsync(userDb, token);
						await _db.SaveChangesAsync(token);

						// Добавление настроек по умолчанию
						await _db.Settings.AddAsync(new Setting() { UserId = userDb.IdUser }, token);

						// Начисление бонуса за регистрацию
						await _db.MoneyBonuses.AddAsync(new MoneyBonuse() { UserId = userDb.IdUser, CollectionTime = DateTime.Now }, token);

						string path = "files/txt/Start.txt";
						var botMessage = await TxtToStringBuilder.FromTxtToStringBuilder(path, token);

						await _bot.SendMessage(chat.Id, botMessage.ToString(), ParseMode.Html, cancellationToken: token);

						//  Изменение состояние юзера в БД
						await userDb.SetStateAsync(BotState.WaitingState, client, _db, update, token);

						await _db.SaveChangesAsync(token);
						return;
					}

					if (await _db.Users.AnyAsync(u => u.TgId == userTg.Id, token))
					{
						var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);

						if (userDb.BotStateId == (int)BotState.WaitingState && msg.Text == "/menu")
						{
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
							await _bot.SendMessage(chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
							return;
						}

						await userDb.DoStateAsync(client, _db, update, token);
						return;
					}
					return;
				}
			case UpdateType.CallbackQuery:
				{
					_onCallbackQuery?.Invoke(client, update);
					var callbackQuery = update.CallbackQuery!;
					var userTg = callbackQuery.From!;

					var currentState = (BotState)(await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token)).BotStateId;

					switch (callbackQuery.Data)
					{
						case "Profile" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

								var userDb = await _db.Users
									.Include(u => u.Games)
									.FirstAsync(u => u.TgId == userTg.Id, token);

								var name = userTg.FirstName + " " + userTg.LastName;
								var score = userDb.Score;
								var maxScore = userDb.MaxScore;
								var totalWinning = userDb.Games.Where(g => g.ResultId == (int)ResultOfGame.Win || g.ResultId == (int)ResultOfGame.Collect).Sum(g => g.Winning);
								var totalLost = userDb.Games.Where(g => g.ResultId == (int)ResultOfGame.Lose).Sum(g => g.Bet);
								var countOfGames = userDb.Games.Count;
								var countOfCollect = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Collect); // Collect
								var countOfWin = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Win); // Win
								var countOfLose = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Lose); // Lose
								var countOfRounds = userDb.Games.Sum(g => g.CountOfRounds);
								var ratingPosition = (await _db.Users.OrderByDescending(u => u.Score).ToListAsync(token)).FindIndex(u => u.TgId == userTg.Id) + 1;

								var botMessage = new StringBuilder();
								botMessage.AppendLine($"👤 Профиль <b>{name}</b> 👤");
								botMessage.AppendLine("");
								botMessage.AppendLine($"<b>Очки 💸</b>");
								botMessage.AppendLine($"— Текущее кол-во очков: <b>{score}</b>");
								botMessage.AppendLine($"— Максимальное кол-во очков: <b>{maxScore}</b>");
								botMessage.AppendLine($"— Всего выиграно очков: <b>{totalLost}</b>");
								botMessage.AppendLine($"— Всего проиграно очков: <b>{totalLost}</b>");
								botMessage.AppendLine("");
								botMessage.AppendLine("<b>Игры-раунды 🎮</b>");
								botMessage.AppendLine($"— Кол-во игр: <b>{countOfGames}</b>");
								botMessage.AppendLine($"— Кол-во выигранных игр: <b>{countOfWin}</b>");
								botMessage.AppendLine($"— Кол-во сборов: <b>{countOfCollect}</b>");
								botMessage.AppendLine($"— Кол-во проигранных игр: <b>{countOfLose}</b>");
								botMessage.AppendLine($"— Кол-во раундов: <b>{countOfRounds}</b>");
								botMessage.AppendLine("");
								botMessage.AppendLine($"🏆 <b>Позиция</b> в рейтинге: <b>{ratingPosition}</b> место 🏆");

								await _bot.SendMessage(chat.Id, botMessage.ToString(), ParseMode.Html, cancellationToken: token);
								return;
							}
						case "Rules" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

								string path = "files/txt/Rules.txt";
								var botMessage = await TxtToStringBuilder.FromTxtToStringBuilder(path, token);

								await _bot.SendMessage(chat.Id, botMessage.ToString(), ParseMode.Html, cancellationToken: token);
								return;
							}
						case "History" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

								var botMessage = new StringBuilder();
								botMessage.AppendLine("👾 <b>История игр</b> 👾");
								botMessage.AppendLine("");

								var games = await _db.Games
									.Include(g => g.Result)
									.Include(g => g.User)
									.Where(g => g.User.TgId == userTg.Id && g.ResultId != null)
									.Take(10)
									.ToListAsync(token);

								if (games.Count == 0)
								{
									botMessage.AppendLine("✖️ История игр пуста ✖️");
								}

								for (int i = 0; i < games.Count; i++)
								{
									var game = games[i];
									botMessage.AppendLine($"{i + 1}. <b>{game.Result!.Title}</b> | Раунды: <b>{game.CountOfRounds}</b> | Выигрыш: <b>{game.Winning}</b> | Ставка: <b>{game.Bet}</b>");
								}

								botMessage.AppendLine();

								var diff = games.Sum(g => g.Winning) - games.Sum(g => g.Bet);
								botMessage.AppendLine(diff switch
								{
									> 0 => $"🤩 За последние 10 игр Вы заработали <b>{diff}</b> очков 🤩",
									0 => $"🤨 За последние 10 игр Вы <b>ничего</b> не заработали 🤨",
									< 0 => $"😟 За последние 10 игр Вы проиграли <b>{diff * -1}</b> очков 😟"
								});

								await _bot.SendMessage(chat.Id, botMessage.ToString(), ParseMode.Html, cancellationToken: token);
								return;
							}
						case "Rating" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

								var userList = await _db.Users
									.Include(u => u.Games)
									.OrderByDescending(u => u.Score)
									.Take(10)
									.ToListAsync(token);

								var botMessage = new StringBuilder();
								botMessage.AppendLine("🏆 <b>Рейтинг игроков по очкам</b> 🏆");
								botMessage.AppendLine("");

								int countOfWin, countOfCollect, countOfLose;
								for (int i = 0; i < userList.Count; i++)
								{
									// Повторение кода
									var user = userList[i];
									countOfWin = user.Games.Count(g => g.ResultId == (int)ResultOfGame.Win);
									countOfCollect = user.Games.Count(g => g.ResultId == (int)ResultOfGame.Collect);
									countOfLose = user.Games.Count(g => g.ResultId == (int)ResultOfGame.Lose);
									botMessage.AppendLine($"{i + 1}. {user.FirstName}: <b>{user.Score}</b> ({user.MaxScore}) очков. <i>W/C/L: {countOfWin}/{countOfCollect}/{countOfLose}.</i>");
								}

								// Повторение кода
								var userDb = await _db.Users.Include(u => u.Games).FirstAsync(u => u.TgId == userTg.Id, token);
								var ratingPosition = (await _db.Users.OrderByDescending(u => u.Score).ToListAsync()).FindIndex(u => u.TgId == userTg.Id) + 1;
								countOfWin = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Win);
								countOfCollect = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Collect);
								countOfLose = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Lose);

								botMessage.AppendLine("");
								botMessage.AppendLine("👤 <b>Ваш рейтинг</b> 👤");
								botMessage.AppendLine($"{ratingPosition}. {userDb.FirstName}: <b>{userDb.Score}</b> ({userDb.MaxScore}) очков. <i>W/C/L: {countOfWin}/{countOfCollect}/{countOfLose}.</i>");

								await _bot.SendMessage(chat.Id, botMessage.ToString(), ParseMode.Html, cancellationToken: token);
								return;
							}
						case "Bonus" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

								var userDb = await _db.Users.Include(u => u.MoneyBonuses).FirstAsync(u => u.TgId == userTg.Id, token);
								var mb = userDb.MoneyBonuses.OrderBy(mb => mb.IdMoneyBonus).Last();

								string botMessage;
								if ((DateTime.Now - mb.CollectionTime).Hours >= 3)
								{
									userDb.Score += 500;
									var now = DateTime.Now;
									mb.CollectionTime = now;
									botMessage = $"Вы успешно получили бонус в размере 500 очков.\nВ следующий раз бонус доступен {now.AddHours(3)}.";
									_db.Users.Update(userDb);
									_db.MoneyBonuses.Update(mb);
									await _db.SaveChangesAsync(token);
								}
								else
								{
									botMessage = $"Слишком рано для получения бонуса.\nБонус будет доступен {mb.CollectionTime.AddHours(3)}.";
								}

								await _bot.AnswerCallbackQuery(callbackQuery.Id, botMessage, showAlert: true, cancellationToken: token);
								return;
							}
						case "Settings" when currentState == BotState.WaitingState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id);
								await userDb.SetStateAsync(BotState.SettingsState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case "Play" when currentState == BotState.WaitingState || currentState == BotState.CollectState || currentState == BotState.WinState || currentState == BotState.LoseState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								if (!_db.Games.Any(g => g.UserId == userDb.IdUser && g.ResultId == null))
									await userDb.SetStateAsync(BotState.BetState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case "Shot" when currentState == BotState.ChoiceState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								var game = await _db.Games
									.Include(g => g.BulletsInGames)
									.FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);

								if (game.BulletsInGames.Any(item => item.IndexOfBullet == game.CountOfRounds))
								{
									var botMessage = "Вы спустили курок... <b>Произошел выстрел... 💀💥🔫</b>";
									await _bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, ParseMode.Html, cancellationToken: token);
									await userDb.SetStateAsync(BotState.LoseState, client, _db, update, token);
								}
								else
								{
									game.Winning = (int)Math.Round(game.Winning * MultiplierFactory.GetMultiplier(game.BulletsInGames.Count), MidpointRounding.AwayFromZero);
									var botMessage = "Вы спустили курок... <b>Выстрела не последовало... 😮💨🔫</b>";
									await _bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, ParseMode.Html, cancellationToken: token);
									await userDb.SetStateAsync(BotState.WinOrChoiceState, client, _db, update, token);
								}

								await _db.SaveChangesAsync(token);
								return;
							}
						case "Collect" when currentState == BotState.ChoiceState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								await userDb.SetStateAsync(BotState.CollectState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case "ToWaitingState" when currentState != BotState.WaitingState && currentState != BotState.ChoiceState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id);
								await userDb.SetStateAsync(BotState.WaitingState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case "SetBulletsType" when currentState == BotState.SettingsState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								await userDb.SetStateAsync(BotState.SetBulletsTypeState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case "SetBulletsCount" when currentState == BotState.SettingsState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								await userDb.SetStateAsync(BotState.SetBulletsCountState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case "SetBulletsTypeTo_Common" when currentState == BotState.SetBulletsTypeState:
						case "SetBulletsTypeTo_Copper" when currentState == BotState.SetBulletsTypeState:
						case "SetBulletsTypeTo_Silver" when currentState == BotState.SetBulletsTypeState:
						case "SetBulletsTypeTo_Golden" when currentState == BotState.SetBulletsTypeState:
						case "SetBulletsTypeTo_Platinum" when currentState == BotState.SetBulletsTypeState:
							{
								await HandleCallbackChangeTypeAsync(_bot, _db, update, token);
								return;
							}
						case "SetBulletsCountTo_1" when currentState == BotState.SetBulletsCountState:
						case "SetBulletsCountTo_2" when currentState == BotState.SetBulletsCountState:
						case "SetBulletsCountTo_3" when currentState == BotState.SetBulletsCountState:
						case "SetBulletsCountTo_4" when currentState == BotState.SetBulletsCountState:
						case "SetBulletsCountTo_5" when currentState == BotState.SetBulletsCountState:
						case "SetBulletsCountTo_6" when currentState == BotState.SetBulletsCountState:
							{
								await HandleCallbackChangeCountAsync(_bot, _db, update, token);
								return;
							}
						default:
							{
								var chat = callbackQuery.Message!.Chat;
								var botMessage = "❌ Недопустимая операция. ❌";
								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
					}
				}
			default:
				{
					return;
				}
		}
	}

	private static void AnyMessage(ITelegramBotClient client, Update update)
	{
		var msg = update.Message!;
		var userTg = msg.From!;
		Console.WriteLine($"{userTg.FirstName} ({userTg.Id}) отправил сообщение: [{msg.Text}]");
	}

	private static void AnyCallbackQuery(ITelegramBotClient client, Update update)
	{
		var callbackQuery = update.CallbackQuery!;
		var userTg = callbackQuery.From;
		Console.WriteLine($"{userTg.FirstName} ({userTg.Id}) нажал на кнопку: [{callbackQuery.Data}]");
	}

	private static async Task HandleCallbackChangeTypeAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
	{
		var callbackQuery = update.CallbackQuery!;
		var userDb = await db.Users.FirstAsync(u => u.TgId == callbackQuery.From.Id, token);
		var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);

		var typeOfBulletId = (int)Enum.Parse<TypeOfBullet>(callbackQuery.Data!.Split("_")[^1]);
		var typeOfBullet = await db.TypesOfBullets.FirstAsync(t => t.IdTypeOfBullet == typeOfBulletId, token);
		string botMessage;

		if (typeOfBullet.Price > userDb.Score)
		{
			botMessage = "<b>Не хватает монет. Возвращение в меню настроек. ❌💸⚙️</b>";
			await userDb.SetStateAsync(BotState.SettingsState, bot, db, update, token);
			await db.SaveChangesAsync(token);
		}
		else
		{
			botMessage = "<b>Настройки успешно изменены. ✅⚙️</b>";
			settings.TypeOfBulletId = typeOfBulletId;
			db.Settings.Update(settings);
		}

		await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, ParseMode.Html, cancellationToken: token);

		await userDb.SetStateAsync(BotState.WaitingState, bot, db, update, token);
		await db.SaveChangesAsync(token);
	}

	private static async Task HandleCallbackChangeCountAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
	{
		var callbackQuery = update.CallbackQuery!;
		var userDb = await db.Users.FirstAsync(u => u.TgId == update.CallbackQuery!.From.Id, token);
		var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);

		settings.CountOfBullets = Convert.ToInt16(callbackQuery.Data!.Split("_")[^1]);
		db.Settings.Update(settings);

		var botMessage = "<b>Настройки успешно изменены. ✅⚙️</b>";
		await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, ParseMode.Html, cancellationToken: token);

		await userDb.SetStateAsync(BotState.WaitingState, bot, db, update, token);
		await db.SaveChangesAsync(token);
	}
}