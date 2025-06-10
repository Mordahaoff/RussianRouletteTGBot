namespace RussianRouletteTGBot.Models;

enum BotState
{
    WaitingState = 1,
    BetState,
    ChoiceState,
    CollectState,
    WinState,
    LoseState,
    ChangeBulletState,
    ChangeCountState
}