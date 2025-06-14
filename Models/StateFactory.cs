namespace RussianRouletteTGBot.Models;

public static class StateFactory
{
    private static readonly Dictionary<BotState, State> _states = new()
    {
        { BotState.WaitingState, new WaitingState() },
        { BotState.BetState, new BetState() },
        { BotState.ChoiceState, new ChoiceState() },
        { BotState.CollectState, new CollectState() },
        { BotState.WinState, new WinState() },
        { BotState.LoseState, new LoseState() },
        { BotState.SettingsState, new SettingsState() },
        { BotState.SetBulletsTypeState, new SetBulletsTypeState() },
        { BotState.SetBulletsCountState, new SetBulletsCountState() },
    };

    public static State GetState(BotState botState)
    {
        if (_states.TryGetValue(botState, out var instance))
            return instance;
        throw new ArgumentException($"Unknown state: {botState}");
    }
}