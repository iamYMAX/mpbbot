namespace TelegramGigaChatBot.Models;

public enum ThinkingMode
{
    Rational,
    Hard,
    Provocative
}

public enum ReplyStyle
{
    Formal,
    Business,
    Neutral,
    Friendly,
    Concise,
    Custom
}

public static class ThinkingModeHelper
{
    private static readonly Dictionary<string, ThinkingMode> ModeMap = new()
    {
        { "rational", ThinkingMode.Rational },
        { "hard", ThinkingMode.Hard },
        { "provocative", ThinkingMode.Provocative }
    };

    private static readonly Dictionary<ThinkingMode, string> StylePrompts = new()
    {
        [ThinkingMode.Rational] = "Думай системно. Разбирай причины, зависимости и сценарии.",
        [ThinkingMode.Hard] = "Не смягчай. Дави на ответственность и реальные последствия.",
        [ThinkingMode.Provocative] = "Ломай убеждения. Ставь под сомнение мотивы и самообман."
    };

    public static bool TryParse(string modeName, out ThinkingMode mode)
    {
        return ModeMap.TryGetValue(modeName.ToLowerInvariant(), out mode);
    }

    public static string GetStylePrompt(ThinkingMode mode)
    {
        return StylePrompts.GetValueOrDefault(mode, StylePrompts[ThinkingMode.Rational]);
    }

    public static string GetModeName(ThinkingMode mode)
    {
        return mode switch
        {
            ThinkingMode.Rational => "Рационально",
            ThinkingMode.Hard => "Жёстко",
            ThinkingMode.Provocative => "Провокационно",
            _ => "Рационально"
        };
    }
}

public static class ReplyStyleHelper
{
    private static readonly Dictionary<ReplyStyle, string> StylePrompts = new()
    {
        [ReplyStyle.Formal] = "Используй строгий, официальный язык. Обращение на 'Вы'.",
        [ReplyStyle.Business] = "Деловой, но не слишком формальный. Сосредоточься на решении.",
        [ReplyStyle.Neutral] = "Нейтральный, сдержанный тон. Без эмоций.",
        [ReplyStyle.Friendly] = "Дружелюбный, позитивный тон. Можно использовать 'ты', если уместно.",
        [ReplyStyle.Concise] = "Максимально кратко и по делу. Только суть.",
        [ReplyStyle.Custom] = "Используй кастомный стиль, указанный пользователем."
    };

    public static string GetStylePrompt(ReplyStyle style)
    {
        return StylePrompts.GetValueOrDefault(style, StylePrompts[ReplyStyle.Neutral]);
    }
}
