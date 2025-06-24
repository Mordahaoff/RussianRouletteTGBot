using Telegram.Bot.Types.ReplyMarkups;

namespace RussianRouletteTGBot;

public static class MenuKeyboard
{
    public static InlineKeyboardMarkup GetKeyboard()
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