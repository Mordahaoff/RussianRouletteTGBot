using Microsoft.EntityFrameworkCore;
using RussianRouletteTGBot;
using RussianRouletteTGBot.Models;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
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

					if (!(await _db.Users.AnyAsync(u => u.TgId == userTg.Id, token) && msg.Text == "/start"))
					{
						// Добавление нового юзера
						var userDb = new User() { TgId = userTg.Id };
						await _db.Users.AddAsync(userDb, token);
						await _db.SaveChangesAsync(token);

						// Добавление настроек по умолчанию
						await _db.Settings.AddAsync(new Setting() { UserId = userDb.IdUser }, token);

						// Вывод информации о возможностях бота при команде /start
						string botMessage = "Информация о возможностях бота.";
						await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);

						//  Изменение состояние юзера в БД
						await userDb.SetStateAsync(BotState.WaitingState, client, _db, update, token);
						await _db.SaveChangesAsync(token);
						return;
					}

					if (await _db.Users.AnyAsync(u => u.TgId == userTg.Id, token))
					{
						var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
						switch ((BotState)userDb.BotStateId)
						{
							case BotState.WaitingState:
								{
									await userDb.DoStateAsync(client, _db, update, token);
									return;
								}
							case BotState.BetState:
								{
									await userDb.DoStateAsync(client, _db, update, token);
									return;
								}
						}
					}
					return;
				}
			case UpdateType.CallbackQuery:
				{
					_onCallbackQuery?.Invoke(client, update);
					var callbackQuery = update.CallbackQuery!;
					var userTg = callbackQuery.From!;
					var chat = callbackQuery.Message!.Chat;

					var currentState = (BotState)(await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token)).BotStateId;

					switch (callbackQuery.Data)
					{
						case "Profile" when currentState == BotState.WaitingState:
							{
								var botMessage = new StringBuilder("CallbackQuery Profile : Информация о профиле.");
								var userDb = await _db.Users
									.Include(u => u.Games)
									.FirstAsync(u => u.TgId == userTg.Id, token);

								var name = userTg.FirstName + " " + userTg.LastName;
								var score = userDb.Score;
								var maxScore = userDb.MaxScore;
								var countOfRounds = userDb.Games.Sum(g => g.CountOfRounds);
								var countOfWin = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Win); // Win
								var countOfLose = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Lose); // Lose
								var countOfCollect = userDb.Games.Count(g => g.ResultId == (int)ResultOfGame.Collect); // Collect

								botMessage.AppendLine($"Никнейм: {name}");
								botMessage.AppendLine($"Всего очков: {score}");
								botMessage.AppendLine($"Максимум очков: {maxScore}");
								botMessage.AppendLine($"Всего раундов: {countOfRounds}");
								botMessage.AppendLine($"Всего побед: {countOfWin}");
								botMessage.AppendLine($"Всего поражений: {countOfLose}");
								botMessage.AppendLine($"Всего сборов: {countOfCollect}");

								await _bot.SendMessage(chat.Id, botMessage.ToString(), cancellationToken: token);
								return;
							}
						case "Rules" when currentState == BotState.WaitingState:
							{
								var botMessage = "CallbackQuery Rules : Информация о правилах.";
								botMessage += "Правила";
								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "History" when currentState == BotState.WaitingState:
							{
								var botMessage = new StringBuilder("CallbackQuery History : Информация об истории.");

								var games = await _db.Games
									.Include(g => g.Result)
									.Include(g => g.User)
									.Where(g => g.User.TgId == userTg.Id && g.ResultId != null)
									.ToListAsync(token);

								foreach (var game in games)
								{
									botMessage.AppendLine($"ID:{game.IdGame} | {game.Result!.Title}");
								}

								await _bot.SendMessage(chat.Id, botMessage.ToString(), cancellationToken: token);
								return;
							}
						case "Settings" when currentState == BotState.WaitingState:
							{
								var userDb = await _db.Users
									.Include(u => u.Settings)
										.ThenInclude(s => s.TypeOfBullet)
									.FirstAsync(u => u.TgId == userTg.Id, token);

								var settings = userDb.Settings.First();

								var botMessage = $"Ваши настройки:" +
									$"Пуля: {settings.TypeOfBullet.Title} | {settings.TypeOfBullet.Multiplier} | {settings.TypeOfBullet.Price}" +
									$"Кол-во: {settings.CountOfBullets}";

								var inlineKeyboard = new InlineKeyboardMarkup([
									[
										InlineKeyboardButton.WithCallbackData("Тип пули", "ChangeBulletsType"),
									],
									[
										InlineKeyboardButton.WithCallbackData("Кол-во пуль", "ChangeBulletsCount"),
									]
								]);

								await _bot.SendMessage(chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
								return;
							}
						case "Achievements" when currentState == BotState.WaitingState:
							{
								var botMessage = new StringBuilder("CallbackQuery Achievements : Информация о достижениях.");
								var userDb = await _db.Users
									.Include(u => u.UserAchievements)
										.ThenInclude(ua => ua.Achievement)
									.FirstAsync(u => u.TgId == userTg.Id, token);

								foreach (var ua in userDb.UserAchievements)
								{
									var a = ua.Achievement;
									botMessage.AppendLine($"ID:{a.IdAchievement} | {a.Title} | {a.Description} | {ua.DateReceived}");
								}

								await _bot.SendMessage(chat.Id, botMessage.ToString(), cancellationToken: token);
								return;
							}
						case "Play" when currentState == BotState.WaitingState || currentState == BotState.CollectState || currentState == BotState.WinState || currentState == BotState.LoseState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								await userDb.SetStateAsync(BotState.BetState, client, _db, update, token);
								// var botMessage = "CallbackQuery Play : Играть. Выберите ставку.";
								// var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								// userDb.BotStateId = (int)BotState.BetState;
								// _db.Users.Update(userDb);
								// await _db.SaveChangesAsync(token);
								// await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "Bonus" when currentState == BotState.WaitingState:
							{
								await _bot.SendMessage(chat.Id, "CallbackQuery Bonus : Получение бонуса.", cancellationToken: token);
								var userDb = await _db.Users.Include(u => u.MoneyBonuses).FirstAsync(u => u.TgId == userTg.Id, token);
								var mb = userDb.MoneyBonuses.OrderBy(mb => mb.IdMoneyBonus).Last();

								string botMessage;
								if ((DateTime.Now - mb.CollectionTime).Hours >= 3)
								{
									botMessage = "Поздравляю! Вы получаете 500 монет!";
									userDb.Score += 500;
									mb.CollectionTime = DateTime.Now;
									_db.Users.Update(userDb);
									_db.MoneyBonuses.Update(mb);
									await _db.SaveChangesAsync(token);
								}
								else
								{
									botMessage = "Вы не получаете бонус. Не прошло достаточно времени";
								}

								await _bot.AnswerCallbackQuery(callbackQuery.Id, botMessage, showAlert: true, cancellationToken: token);
								return;
							}
						case "ChangeBulletsType" when currentState == BotState.WaitingState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								userDb.BotStateId = (int)BotState.ChangeBulletsTypeState;
								_db.Update(userDb);
								await _db.SaveChangesAsync(token);

								var bulletsTypeList = await _db.TypesOfBullets.ToListAsync(token);

								var botMessage = new StringBuilder($"Выберите один из доступных типов пуль:");
								foreach (var type in bulletsTypeList)
								{
									botMessage.AppendLine($"{type.Title} | {type.Multiplier} | {type.Price}");
								}

								var inlineKeyboard = new InlineKeyboardMarkup([
									[
										InlineKeyboardButton.WithCallbackData("Обычная", "ChangeBulletTo_Common"),
										InlineKeyboardButton.WithCallbackData("Медная", "ChangeBulletTo_Copper"),
										InlineKeyboardButton.WithCallbackData("Серебряная", "ChangeBulletTo_Silver"),
									],
									[
										InlineKeyboardButton.WithCallbackData("Золотая", "ChangeBulletTo_Golden"),
										InlineKeyboardButton.WithCallbackData("Платиновая", "ChangeBulletTo_Platinum"),
									]
								]);

								await _bot.SendMessage(chat.Id, botMessage.ToString(), replyMarkup: inlineKeyboard, cancellationToken: token);
								return;
							}
						case "ChangeBulletsCount" when currentState == BotState.WaitingState:
							{
								var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);
								userDb.BotStateId = (int)BotState.ChangeBulletsCountState;
								_db.Update(userDb);
								await _db.SaveChangesAsync(token);

								var botMessage = "Выберите число от 1 до 6";

								var inlineKeyboard = new InlineKeyboardMarkup([
									[
										InlineKeyboardButton.WithCallbackData("1", "ChangeCountTo_1"),
										InlineKeyboardButton.WithCallbackData("2", "ChangeCountTo_2"),
										InlineKeyboardButton.WithCallbackData("3", "ChangeCountTo_3"),
									],
									[
										InlineKeyboardButton.WithCallbackData("4", "ChangeCountTo_4"),
										InlineKeyboardButton.WithCallbackData("5", "ChangeCountTo_5"),
										InlineKeyboardButton.WithCallbackData("6", "ChangeCountTo_6"),
									]
								]);

								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "ChangeBulletTo_Common" when currentState == BotState.ChangeBulletsTypeState:
						case "ChangeBulletTo_Copper" when currentState == BotState.ChangeBulletsTypeState:
						case "ChangeBulletTo_Silver" when currentState == BotState.ChangeBulletsTypeState:
						case "ChangeBulletTo_Golden" when currentState == BotState.ChangeBulletsTypeState:
						case "ChangeBulletTo_Platinum" when currentState == BotState.ChangeBulletsTypeState:
							{
								await HandleCallbackChangeTypeAsync(_db, callbackQuery.Data, userTg.Id, token);
								return;
							}
						case "ChangeCountTo_1" when currentState == BotState.ChangeBulletsCountState:
						case "ChangeCountTo_2" when currentState == BotState.ChangeBulletsCountState:
						case "ChangeCountTo_3" when currentState == BotState.ChangeBulletsCountState:
						case "ChangeCountTo_4" when currentState == BotState.ChangeBulletsCountState:
						case "ChangeCountTo_5" when currentState == BotState.ChangeBulletsCountState:
						case "ChangeCountTo_6" when currentState == BotState.ChangeBulletsCountState:
							{
								await HandleCallbackChangeCountAsync(_db, callbackQuery.Data, userTg.Id, token);
								return;
							}
						default:
							{
								var botMessage = "Недопустимая операция.";
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

	private static async Task HandleCallbackChangeTypeAsync(RouletteContext db, string callbackData, long userTgId, CancellationToken token)
	{
		var userDb = await db.Users.FirstAsync(u => u.TgId == userTgId, token);
		var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);

		userDb.BotStateId = (int)BotState.WaitingState;
		settings.TypeOfBulletId = (int)Enum.Parse<TypeOfBullet>(callbackData.Split("_")[^1]);

		db.Users.Update(userDb);
		db.Settings.Update(settings);
		await db.SaveChangesAsync(token);
	}

	private static async Task HandleCallbackChangeCountAsync(RouletteContext db, string callbackData, long userTgId, CancellationToken token)
	{
		var userDb = await db.Users.FirstAsync(u => u.TgId == userTgId, token);
		var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);

		userDb.BotStateId = (int)BotState.WaitingState;
		settings.CountOfBullets = Convert.ToInt16(callbackData[^1]);

		db.Users.Update(userDb);
		db.Settings.Update(settings);
		await db.SaveChangesAsync(token);
	}
}