namespace RussianRouletteTGBot.Models;

public enum BotState
{
    WaitingState = 1,
    BetState,
    ChoiceState,
    CollectState,
    WinState,
    LoseState,
    SettingsState,
    ChangeBulletsTypeState,
    ChangeBulletsCountState
}

public enum TypeOfBullet
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