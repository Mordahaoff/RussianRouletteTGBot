using Microsoft.EntityFrameworkCore;
using RussianRouletteTGBot;
using RussianRouletteTGBot.Models;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
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
					var user = msg.From!;
					var chat = msg.Chat!;

					Console.WriteLine($"{user.FirstName} ({user.Id}) написал сообщение: {msg.Text}");

					if (!_db.Users.Any(u => u.TgId == user.Id) && msg.Text == "/start")
					{
						await _db.Users.AddAsync(new User() { TgId = user.Id });
						await _db.SaveChangesAsync();

						int findUserId = (await _db.Users.FirstAsync(u => u.TgId == user.Id)).IdUser;
						await _db.Settings.AddAsync(new Setting() { UserId = findUserId });

						string botMessage = "Информация о возможностях бота.";
						await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
						return;
					}

					if (_db.Users.Any(u => u.TgId == user.Id))
					{
						var userDb = await _db.Users.FirstAsync(u => u.TgId == user.Id);
						switch ((BotState)userDb.BotStateId)
						{
							case BotState.WaitingState:
								{
									var botMessage = "WaitingState : Перечень возможных команд.";
									var inlineKeyboard = new InlineKeyboardMarkup(new[]
										{
											// first row
											new []
											{
												InlineKeyboardButton.WithCallbackData("Профиль", "Profile"),
												InlineKeyboardButton.WithCallbackData("Достижения", "Achievements"),
											},
											// second row
											new []
											{
												InlineKeyboardButton.WithCallbackData("Бонус", "Bonus"),
												InlineKeyboardButton.WithCallbackData("История", "History"),

											},
											new[]
											{
												InlineKeyboardButton.WithCallbackData("Правила", "Rules"),
												InlineKeyboardButton.WithCallbackData("Настройки", "Settings"),
											},
											new[]
											{
												InlineKeyboardButton.WithCallbackData("Играть", "Play"),
											},
										});
									await _bot.SendMessage(chat.Id, botMessage, replyMarkup: inlineKeyboard, cancellationToken: token);
									return;
								}
							case BotState.BetState:
								{
									// if (int.Parse(msg.Text, out int result) && result > && result <= userDb.MaxScore) ;

									// var botMessage = "BetState : Выберите ставку.";
									// await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
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
					var user = callbackQuery.From!;
					var chat = callbackQuery.Message!.Chat;

					Console.WriteLine($"{user.FirstName} ({user.Id}) нажал на кнопку: [{callbackQuery.Data}]");

					switch (callbackQuery.Data)
					{
						case "Profile":
							{
								var botMessage = "CallbackQuery Profile : Информация о профиле.";
								// Имя, кол-во очков, кол-во игр, кол-во раундов, кол-во побед, кол-во поражений, кол-во сбора
								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "Rules":
							{
								var botMessage = "CallbackQuery Rules : Информация о правилах.";
								// Правила есть в Google Drive
								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "History":
							{
								var botMessage = new StringBuilder("CallbackQuery History : Информация об истории.");

								var games = _db.Games
									.Include(g => g.Result)
									.Include(g => g.User)
									.Where(g => g.User.TgId == user.Id && g.ResultId != null)
									.ToList();

								foreach (var game in games)
								{
									botMessage.AppendLine($"ID:{game.IdGame} | {game.Result!.Title}");
								}

								await _bot.SendMessage(chat.Id, botMessage.ToString(), cancellationToken: token);
								return;
							}
						case "Settings":
							{
								var botMessage = "CallbackQuery Settings : Информация о настройках.";
								// Вывод настроек игры (тип пули, кол-во патронов)
								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "Achievements":
							{
								var botMessage = new StringBuilder("CallbackQuery Achievements : Информация о достижениях.");
								var userDb = _db.Users
									.Include(u => u.UserAchievements)
										.ThenInclude(ua => ua.Achievement)
									.First(u => u.TgId == user.Id);

								foreach (var ua in userDb.UserAchievements)
								{
									var a = ua.Achievement;
									botMessage.AppendLine($"ID:{a.IdAchievement} | {a.Title} | {a.Description} | {ua.DateReceived}");
								}

								await _bot.SendMessage(chat.Id, botMessage.ToString(), cancellationToken: token);
								return;
							}
						case "Play":
							{
								var botMessage = "CallbackQuery Play : Играть. Выберите ставку.";
								var userDb = _db.Users.First(u => u.TgId == user.Id);
								userDb.BotStateId = 2;
								_db.Users.Update(userDb);
								await _db.SaveChangesAsync();
								await _bot.SendMessage(chat.Id, botMessage, cancellationToken: token);
								return;
							}
						case "Bonus":
							{
								var botMessage = new StringBuilder("CallbackQuery Bonus : Получение бонуса.");
								var userDb = _db.Users.Include(u => u.MoneyBonuses).First(u => u.TgId == user.Id);
								var mb = userDb.MoneyBonuses.OrderBy(mb => mb.IdMoneyBonus).Last();

								if ((DateTime.Now - mb.CollectionTime).Hours >= 3)
								{
									botMessage.AppendLine("Поздравляю! Вы получаете 500 монет!");
									userDb.Score += 500;
									mb.CollectionTime = DateTime.Now;
									_db.Users.Update(userDb);
									_db.MoneyBonuses.Update(mb);
									await _db.SaveChangesAsync();
								}
								else
								{
									botMessage.AppendLine("Вы не получаете бонус. Не прошло достаточно времени");
								}

								await _bot.SendMessage(chat.Id, botMessage.ToString(), cancellationToken: token);
								return;
							}
					}

					return;
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
		var user = msg.From!;
		Console.WriteLine($"{user.FirstName} ({user.Id}) отправил сообщение: [{msg.Text}]");
	}

	private static void AnyCallbackQuery(ITelegramBotClient client, Update update)
	{
		var callbackQuery = update.CallbackQuery!;
		var user = callbackQuery.From;
		Console.WriteLine($"{user.FirstName} ({user.Id}) нажал на кнопку: [{callbackQuery.Data}]");
	}
}