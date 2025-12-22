namespace TelegramGigaChatBot.Models;

public enum ThinkingMode
{
    Rational,
    Hard,
    Provocative
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
