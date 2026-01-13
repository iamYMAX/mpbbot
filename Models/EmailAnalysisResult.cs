using System.Text.Json.Serialization;

namespace TelegramGigaChatBot.Models;

public class EmailAnalysisResult
{
    [JsonPropertyName("sender_type")]
    public string SenderType { get; set; }

    [JsonPropertyName("theme")]
    public string Theme { get; set; }

    [JsonPropertyName("intent")]
    public string Intent { get; set; }

    [JsonPropertyName("emotional_tone")]
    public string EmotionalTone { get; set; }

    [JsonPropertyName("has_questions")]
    public bool HasQuestions { get; set; }

    [JsonPropertyName("has_deadlines")]
    public bool HasDeadlines { get; set; }

    [JsonPropertyName("has_money_mention")]
    public bool HasMoneyMention { get; set; }

    [JsonPropertyName("has_legal_mention")]
    public bool HasLegalMention { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }

    [JsonPropertyName("priority")]
    public string Priority { get; set; } // "Critical", "High", "Medium", "Low"

    public EmailMessage OriginalMessage { get; set; }
}
