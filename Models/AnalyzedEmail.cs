using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TelegramGigaChatBot.Models
{
    public enum EmailPriority
    {
        Critical,
        High,
        Medium,
        Low
    }

    public class AnalyzedEmail
    {
        [JsonPropertyName("priority")]
        public EmailPriority Priority { get; set; }

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = string.Empty;

        [JsonPropertyName("emotional_tone")]
        public string EmotionalTone { get; set; } = string.Empty;

        [JsonPropertyName("key_data")]
        public Dictionary<string, List<string>> KeyData { get; set; } = new();

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("action_required")]
        public string ActionRequired { get; set; } = string.Empty;

        [JsonPropertyName("risk")]
        public string Risk { get; set; } = string.Empty;

        [JsonIgnore]
        public EmailMessage OriginalMessage { get; set; } = null!;
    }
}
