using Telegram.Bot.Types.ReplyMarkups;

namespace RussianRouletteTGBot.Models;

public static class InlineKeyboards
{
    public static InlineKeyboardMarkup GetMenuKeyboard(bool isAdmin)
    {
        if (isAdmin)
        {
            return new InlineKeyboardMarkup(
                [
                    // first row
                    [
                        InlineKeyboardButton.WithCallbackData("Профиль 👤", "Profile"),
                        InlineKeyboardButton.WithCallbackData("Рейтинг 🏆", "Rating"),
                        InlineKeyboardButton.WithCallbackData("История 👾", "History"),
                    ],
                    // second row
                    [
                        InlineKeyboardButton.WithCallbackData("Правила 📄", "Rules"),
                        InlineKeyboardButton.WithCallbackData("Настройки ⚙️", "Settings"),
                        InlineKeyboardButton.WithCallbackData("Бонус 🎁", "Bonus"),

                    ],
                    // third row
                    [
                        InlineKeyboardButton.WithCallbackData("Играть 🎮", "Play"),
                    ],
                    // fourth row
                    [
                        InlineKeyboardButton.WithCallbackData("⭕️ Админ-панель ⭕️", "AdminPanel"),
                    ],
                ]);
        }
        else
        {
            return new InlineKeyboardMarkup(
                [
                    // first row
                    [
                        InlineKeyboardButton.WithCallbackData("Профиль 👤", "Profile"),
                        InlineKeyboardButton.WithCallbackData("Рейтинг 🏆", "Rating"),
                        InlineKeyboardButton.WithCallbackData("История 👾", "History"),
                    ],
                    // second row
                    [
                        InlineKeyboardButton.WithCallbackData("Правила 📄", "Rules"),
                        InlineKeyboardButton.WithCallbackData("Настройки ⚙️", "Settings"),
                        InlineKeyboardButton.WithCallbackData("Бонус 🎁", "Bonus"),

                    ],
                    // third row
                    [
                        InlineKeyboardButton.WithCallbackData("Играть 🎮", "Play"),
                    ],
                ]);
        }
    }

    public static InlineKeyboardMarkup GetToWaitingStateKeyboard()
        => new([[InlineKeyboardButton.WithCallbackData("[Вернуться]", "ToWaitingState")]]);

    public static InlineKeyboardMarkup GetToAdminPanelKeyboard()
     => new([[InlineKeyboardButton.WithCallbackData("[Админ-панель]", "AdminPanel")]]);

}