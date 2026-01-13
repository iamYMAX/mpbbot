namespace TelegramGigaChatBot.Models;

public enum ReplyStyle
{
    Formal,
    Business,
    Neutral,
    Friendly,
    HardBrief,
    Custom
}

public static class ReplyStyleHelper
{
    private static readonly Dictionary<ReplyStyle, string> StylePrompts = new()
    {
        [ReplyStyle.Formal] = "Используй строгий, официальный язык. Обращение на 'Вы'.",
        [ReplyStyle.Business] = "Деловой, структурированный ответ. Четко и по существу.",
        [ReplyStyle.Neutral] = "Нейтральный, вежливый тон. Без лишних эмоций.",
        [ReplyStyle.Friendly] = "Дружелюбный, неформальный стиль. Можно использовать 'ты'.",
        [ReplyStyle.HardBrief] = "Максимально кратко, жестко и по делу. Без вступлений и прощаний."
    };

    public static string GetStylePrompt(ReplyStyle style, string customStyle = "")
    {
        if (style == ReplyStyle.Custom)
        {
            return string.IsNullOrWhiteSpace(customStyle)
                ? StylePrompts[ReplyStyle.Neutral]
                : customStyle;
        }
        return StylePrompts.GetValueOrDefault(style, StylePrompts[ReplyStyle.Neutral]);
    }
}
