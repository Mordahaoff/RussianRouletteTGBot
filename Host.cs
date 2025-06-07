using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class Host
{
    private readonly ITelegramBotClient _bot;
    private readonly ReceiverOptions _receiverOptions;
    private Action<ITelegramBotClient, Update>? _onMessage;
    // private Action<ITelegramBotClient, Update>? _onCallbackQuery;

    public Host(string token)
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
    }

    public void Start()
    {
        var cts = new CancellationTokenSource();
        _bot.StartReceiving(UpdateHandler, ErrorHandler, _receiverOptions, cts.Token);
        _onMessage = AnyMessage;
        // _onCallbackQuery = AnyCallbackQuery;
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
                    return;
                }
            // case UpdateType.CallbackQuery:
            //     {
            //         _onCallbackQuery?.Invoke(client, update);
            //         return;
            //     }
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

    // private static void AnyCallbackQuery(ITelegramBotClient client, Update update)
    // {
    //     var callbackQuery = update.CallbackQuery!;
    //     var user = callbackQuery.From;
    //     Console.WriteLine($"{user.FirstName} ({user.Id}) нажал на кнопку: [{callbackQuery.Data}]");
    // }
}