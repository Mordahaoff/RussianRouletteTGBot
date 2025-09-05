using Microsoft.EntityFrameworkCore;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using RussianRouletteTGBot.Models.Entities;
using RussianRouletteTGBot.Models.Extensions;
using User = RussianRouletteTGBot.Models.Entities.User;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Telegram.Bot.Types.ReplyMarkups;
using System.Formats.Asn1;

namespace RussianRouletteTGBot.Models;

public class Host
{
	private readonly ITelegramBotClient _bot;
	private readonly ReceiverOptions _receiverOptions;
	private Action<ITelegramBotClient, Update>? _onMessage;
	private Action<ITelegramBotClient, Update>? _onCallbackQuery;
	private readonly RouletteContext _db;
	private const long OWNER_ID = 825165091;

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
						var userDb = new User() { TgId = userTg.Id, FirstName = userTg.FirstName };
						await _db.Users.AddAsync(userDb, token);
						await _db.SaveChangesAsync(token);

						// Добавление настроек по умолчанию
						await _db.Settings.AddAsync(new Entities.Setting() { UserId = userDb.IdUser }, token);
						// Начисление бонуса за регистрацию
						await _db.MoneyBonuses.AddAsync(new MoneyBonuse() { UserId = userDb.IdUser, CollectionTime = DateTime.Now }, token);
						// Добавление записи с информативными сообщениями для этого юзера
						await _db.InfoMessages.AddAsync(new Entities.InfoMessage() { UserId = userDb.IdUser, }, token);
						await _db.SaveChangesAsync(token);

						var template = new Template(token);
						await template.ReadTemplateAsync("files/txt/Start.txt");
						var botMessage = template.GetTemplate();

						await _bot.SendMessage(chat.Id, botMessage.ToString(), ParseMode.Html, cancellationToken: token);

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
					var callbackQueryId = callbackQuery.Id;
					var userTg = callbackQuery.From!;
					var userDb = await _db.Users.FirstAsync(u => u.TgId == userTg.Id, token);

					var currentState = (BotState)userDb.BotStateId;
					switch (currentState)
					{
						case BotState.WaitingState or BotState.AdminPanel_ChangePlayerPointsState when callbackQuery.Data == "AdminPanel":
							{
								await userDb.SetStateAsync(BotState.AdminPanelState, _bot, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case BotState.WaitingState or BotState.CollectState or BotState.WinState or BotState.LoseState when callbackQuery.Data == "Play":
							{
								if (!await _db.Games.AnyAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token))
								{
									await userDb.SetStateAsync(BotState.BetState, client, _db, update, token);
									await _db.SaveChangesAsync(token);
								}
								return;
							}
						case BotState.WaitingState or BotState.SetBulletsTypeState or BotState.SetBulletsCountState when callbackQuery.Data == "Settings":
							{
								await userDb.SetStateAsync(BotState.SettingsState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case BotState.WaitingState:
							{
								var chat = callbackQuery.Message!.Chat;
								switch (callbackQuery.Data)
								{
									case "Profile":
										{
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

											var dict = new Dictionary<string, string> {
												{ "{name}", name },
												{ "{score}", score.ToNumberWithDots() },
												{ "{maxScore}", maxScore.ToNumberWithDots() },
												{ "{totalWinning}", totalWinning.ToNumberWithDots() },
												{ "{totalLost}", totalLost.ToNumberWithDots() },
												{ "{countOfGames}", countOfGames.ToNumberWithDots() },
												{ "{countOfWin}", countOfWin.ToNumberWithDots() },
												{ "{countOfCollect}", countOfCollect.ToNumberWithDots() },
												{ "{countOfLose}", countOfLose.ToNumberWithDots() },
												{ "{countOfRounds}", countOfRounds.ToNumberWithDots() },
												{ "{ratingPosition}", ratingPosition.ToNumberWithDots() }
											};

											var template = new Template(token);
											await template.ReadTemplateAsync("files/txt/Profile.txt");
											template.Format(dict);
											var botMessage = template.GetTemplate();

											await TryEditInfoMessage(_bot, _db, chat.Id, callbackQueryId, userTg.Id, botMessage.ToString(), token);
											return;
										}
									case "Rules":
										{
											var template = new Template(token);
											await template.ReadTemplateAsync("files/txt/Rules.txt");
											var botMessage = template.GetTemplate();

											await TryEditInfoMessage(_bot, _db, chat.Id, callbackQueryId, userTg.Id, botMessage.ToString(), token);
											return;
										}
									case "History":
										{
											var botMessage = new StringBuilder();
											botMessage.AppendLine("👾 <b>История игр</b> 👾");
											botMessage.AppendLine("");

											var games = await _db.Games
												.Include(g => g.Result)
												.Where(g => g.UserId == userDb.IdUser && g.ResultId != null)
												.OrderByDescending(g => g.IdGame)
												.Take(10)
												.ToListAsync(token);

											if (games.Count == 0)
											{
												botMessage.AppendLine("✖️ История игр пуста ✖️");
											}

											for (int i = 0; i < games.Count; i++)
											{
												var game = games[i];
												botMessage.AppendLine($"{i + 1}. <b>{game.Result!.Title}</b> | Раунды: <b>{game.CountOfRounds}</b> | Выигрыш: <b>{game.Winning.ToNumberWithDots()}</b> | Ставка: <b>{game.Bet.ToNumberWithDots()}</b>");
											}

											botMessage.AppendLine();

											var diff = games.Sum(g => g.Winning) - games.Sum(g => g.Bet);
											botMessage.AppendLine(diff switch
											{
												> 0 => $"🤩 За последние 10 игр Вы заработали <b>{diff.ToNumberWithDots()}</b> очка(-ов) 🤩",
												0 => $"🤨 За последние 10 игр Вы <b>ничего</b> не заработали 🤨",
												< 0 => $"😟 За последние 10 игр Вы проиграли <b>{(diff * -1).ToNumberWithDots()}</b> очка(-ов) 😟"
											});

											await TryEditInfoMessage(_bot, _db, chat.Id, callbackQueryId, userTg.Id, botMessage.ToString(), token);
											return;
										}
									case "Rating":
										{
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
												var user = userList[i];
												user.GetGameResultsInfo(out countOfWin, out countOfCollect, out countOfLose);
												botMessage.AppendLine($"{i + 1}. {user.FirstName}: <b>{user.Score.ToNumberWithDots()}</b> ({user.MaxScore.ToNumberWithDots()}) очков. <i>W/C/L: {countOfWin.ToNumberWithDots()}/{countOfCollect.ToNumberWithDots()}/{countOfLose.ToNumberWithDots()}.</i>");
											}

											userDb = await _db.Users.Include(u => u.Games).FirstAsync(u => u.IdUser == userDb.IdUser, token);
											userDb.GetGameResultsInfo(out countOfWin, out countOfCollect, out countOfLose);
											var ratingPosition = (await _db.Users.OrderByDescending(u => u.Score).ToListAsync(token)).FindIndex(u => u.IdUser == userDb.IdUser) + 1;

											botMessage.AppendLine("");
											botMessage.AppendLine("👤 <b>Ваш рейтинг</b> 👤");
											botMessage.AppendLine($"{ratingPosition}. {userDb.FirstName}: <b>{userDb.Score.ToNumberWithDots()}</b> ({userDb.MaxScore.ToNumberWithDots()}) очков. <i>W/C/L: {countOfWin.ToNumberWithDots()}/{countOfCollect.ToNumberWithDots()}/{countOfLose.ToNumberWithDots()}.</i>");

											await TryEditInfoMessage(_bot, _db, chat.Id, callbackQueryId, userTg.Id, botMessage.ToString(), token);
											return;
										}
									case "Bonus":
										{
											var mb = await _db.MoneyBonuses.Include(mb => mb.User).FirstAsync(mb => mb.User.TgId == userTg.Id, token);

											var botMessage = new StringBuilder();
											if ((DateTime.Now - mb.CollectionTime).Hours >= 3)
											{
												mb.User.Score += 500;
												var now = DateTime.Now;
												mb.CollectionTime = now;
												botMessage.AppendLine("🎁✅ Вы успешно получили бонус в размере 500 очков. ✅🎁");
												botMessage.AppendLine($"⏰ В следующий раз бонус доступен {now.AddHours(3)}. ⏰");
												_db.Users.Update(mb.User);
												_db.MoneyBonuses.Update(mb);
												await _db.SaveChangesAsync(token);
											}
											else
											{
												botMessage.AppendLine("🎁❌ Слишком рано для получения бонуса. ❌🎁");
												botMessage.AppendLine($"⏰ Бонус будет доступен {mb.CollectionTime.AddHours(3)}. ⏰");
											}

											await TryEditInfoMessage(_bot, _db, chat.Id, callbackQueryId, userTg.Id, botMessage.ToString(), token);
											return;
										}
								}
								return;
							}
						case not BotState.WaitingState when callbackQuery.Data == "ToWaitingState":
							{
								await userDb.SetStateAsync(BotState.WaitingState, client, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						case BotState.ChoiceState:
							{
								switch (callbackQuery.Data)
								{
									case "Shot":
										{
											var chat = callbackQuery.Message!.Chat;

											var game = await _db.Games
												.Include(g => g.BulletsInGames)
												.FirstAsync(g => g.UserId == userDb.IdUser && g.ResultId == null, token);

											if (game.BulletsInGames.Any(item => item.IndexOfBullet == game.CountOfRounds))
											{
												await userDb.SetStateAsync(BotState.LoseState, client, _db, update, token);
											}
											else
											{
												game.Winning = (int)Math.Round(game.Winning * MultiplierFactory.GetMultiplier(game.BulletsInGames.Count), MidpointRounding.AwayFromZero);
												await userDb.SetStateAsync(BotState.WinOrChoiceState, client, _db, update, token);
											}

											await _db.SaveChangesAsync(token);
											return;
										}
									case "Collect":
										{
											await userDb.SetStateAsync(BotState.CollectState, client, _db, update, token);
											await _db.SaveChangesAsync(token);
											return;
										}
									case "CheckBullets":
										{
											var game = await _db.Games
												.Include(g => g.User)
												.Include(g => g.BulletsInGames)
												.FirstAsync(g => g.User.TgId == userTg.Id && g.ResultId == null, token);

											var botMessage = new StringBuilder("Пули ждут Вас на раундах: ");
											var bullets = game.BulletsInGames.OrderBy(b => b.IndexOfBullet);
											foreach (var bullet in bullets)
											{
												botMessage.Append($"{bullet.IndexOfBullet}");
												if (bullet.IndexOfBullet != bullets.Last().IndexOfBullet)
												{
													botMessage.Append(", ");
												}
												else
												{
													botMessage.Append('.');
												}
											}
											await _bot.AnswerCallbackQuery(callbackQuery.Id, botMessage.ToString(), showAlert: true, cancellationToken: token);
											return;
										}
								}
								return;
							}
						case BotState.SettingsState:
							{
								switch (callbackQuery.Data)
								{
									case "SetBulletsType":
										{
											await userDb.SetStateAsync(BotState.SetBulletsTypeState, client, _db, update, token);
											await _db.SaveChangesAsync(token);
											return;
										}
									case "SetBulletsCount":
										{
											await userDb.SetStateAsync(BotState.SetBulletsCountState, client, _db, update, token);
											await _db.SaveChangesAsync(token);
											return;
										}
								}
								return;
							}
						case BotState.SetBulletsTypeState when callbackQuery.Data!.Split("_")[0] == "SetBulletsTypeTo":
							{
								await HandleCallbackChangeTypeAsync(_bot, _db, update, token);
								return;
							}
						case BotState.SetBulletsCountState when callbackQuery.Data!.Split("_")[0] == "SetBulletsCountTo":
							{
								await HandleCallbackChangeCountAsync(_bot, _db, update, token);
								return;
							}
						case BotState.AdminPanelState when callbackQuery.Data == "AdminPanel_ChangePlayerPointsState":
							{
								await userDb.SetStateAsync(BotState.AdminPanel_ChangePlayerPointsState, _bot, _db, update, token);
								await _db.SaveChangesAsync(token);
								return;
							}
						default:
							{
								var botMessage = "❌ Недопустимая операция. ❌";
								await _bot.AnswerCallbackQuery(callbackQuery.Id, botMessage, showAlert: true, cancellationToken: token);
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
			botMessage = "Ошибка: не хватает монет. ❌💸⚙️";
		}
		else
		{
			botMessage = "Настройки успешно изменены. ✅⚙️";
			settings.TypeOfBulletId = typeOfBulletId;
			db.Settings.Update(settings);
			await db.SaveChangesAsync(token);
		}

		await bot.AnswerCallbackQuery(callbackQuery.Id, botMessage, showAlert: true, cancellationToken: token);
	}

	private static async Task HandleCallbackChangeCountAsync(ITelegramBotClient bot, RouletteContext db, Update update, CancellationToken token)
	{
		var callbackQuery = update.CallbackQuery!;
		var userDb = await db.Users.FirstAsync(u => u.TgId == update.CallbackQuery!.From.Id, token);
		var settings = await db.Settings.FirstAsync(s => s.UserId == userDb.IdUser, token);

		settings.CountOfBullets = Convert.ToInt16(callbackQuery.Data!.Split("_")[^1]);
		db.Settings.Update(settings);
		await db.SaveChangesAsync(token);

		var botMessage = "Настройки успешно изменены. ✅⚙️";
		await bot.AnswerCallbackQuery(callbackQuery.Id, botMessage, showAlert: true, cancellationToken: token);
	}

	private static async Task TryEditInfoMessage(ITelegramBotClient bot, RouletteContext db, long chatId, string callbackQueryId, long userTgId, string botMessage, CancellationToken token)
	{
		var im = await Extensions.InfoMessage.GetInfoMessageAsyncByTgId(db, userTgId, token);
		try
		{
			await bot.EditMessageText(chatId, (int)im.IdMessage!, botMessage.ToString(), ParseMode.Html, replyMarkup: InlineKeyboards.GetMenuKeyboard(userTgId == OWNER_ID), cancellationToken: token);
		}
		catch (RequestException)
		{
			await bot.AnswerCallbackQuery(callbackQueryId, "⚠️ Ошибка: вы уже в этом разделе! ⚠️", true, cancellationToken: token);
		}
	}
}