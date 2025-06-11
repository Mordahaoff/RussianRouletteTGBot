namespace RussianRouletteTGBot.Models;

enum BotState
{
    WaitingState = 1,
    BetState,
    ChoiceState,
    CollectState,
    WinState,
    LoseState,
    ChangeBulletsTypeState,
    ChangeBulletsCountState
}

enum TypeOfBullet
{
    Common = 1,
    Copper,
    Silver,
    Golden,
    Platinum,
}

enum ResultOfGame
{
    Win = 1,
    Lose,
    Collect
}