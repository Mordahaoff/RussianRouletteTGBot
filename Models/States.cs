// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using Telegram.Bot;

// namespace RussianRouletteTGBot.Models;

// public abstract class State
// {
//     public virtual async Task EnterAsync(ITelegramBotClient client, DbContext context)
//     {
//         await Task.CompletedTask;
//     }

//     public virtual async Task DoAsync()
//     {
//         await Task.CompletedTask;
//     }

//     public virtual async Task ExitAsync()
//     {
//         await Task.CompletedTask;
//     }
// }

// public class WaitingState : State
// {
//     public override async Task EnterAsync()
//     {

//         await Task.Delay(500);
//     }

//     public override async Task DoAsync()
//     {
//         await Task.Delay(100);
//     }

//     public override async Task ExitAsync()
//     {
//         await Task.Delay(100);
//     }
// }