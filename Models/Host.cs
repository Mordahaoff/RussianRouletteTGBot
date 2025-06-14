using Microsoft.EntityFrameworkCore;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using RussianRouletteTGBot.Models.Entities;
using User = RussianRouletteTGBot.Models.Entities.User;

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
						var userDb = new User() { TgId = userTg.Id, Score = 500 };
						await _db.Users.AddAsync(userDb, token);
						await _db.SaveChangesAsync(token);

						// Добавление настроек по умолчанию
						await _db.Settings.AddAsync(new Setting() { UserId = userDb.IdUser }, token);

						// Начисление бонуса за регистрацию
						await _db.MoneyBonuses.AddAsync(new MoneyBonuse() { UserId = userDb.IdUser, CollectionTime = DateTime.Now }, token);

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
								var chat = callbackQuery.Message!.Chat;

								var botMessage = "CallbackQuery Rules : Информация о правилах.";
								botMessage += "Правила";
								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "History" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

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
						case "Achievements" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

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
						case "Bonus" when currentState == BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;

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
									.FirstAsync(g => g.UserId == userDb.IdUser, token);

								if (game.BulletsInGames.Any(item => item.IndexOfBullet == game.CountOfRounds++))
								{
									await userDb.SetStateAsync(BotState.LoseState, client, _db, update, token);
								}
								else
								{
									var botMessage = "Вы спустили курок. Выстрела не последовало.";
									await _bot.SendMessage(update.Message!.Chat.Id, botMessage, cancellationToken: token);
									await userDb.SetStateAsync(BotState.ChoiceState, client, _db, update, token);
								}

								_db.Games.Update(game);
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
			botMessage = "Не хватает монет. Возвращение в меню";
		}
		else
		{
			botMessage = "Настройки успешно изменены.";
			settings.TypeOfBulletId = typeOfBulletId;
			db.Settings.Update(settings);
		}

		await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, cancellationToken: token);

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

		var botMessage = "Настройки успешно изменены.";
		await bot.SendMessage(callbackQuery.Message!.Chat.Id, botMessage, cancellationToken: token);

		await userDb.SetStateAsync(BotState.WaitingState, bot, db, update, token);
		await db.SaveChangesAsync(token);
	}
}