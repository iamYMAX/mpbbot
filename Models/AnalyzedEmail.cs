using System.Text.Json.Serialization;

namespace TelegramGigaChatBot.Models
{
    public class AnalyzedEmail
    {
        [JsonPropertyName("importance")]
        public string Importance { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = string.Empty;

        [JsonPropertyName("action_required")]
        public string ActionRequired { get; set; } = string.Empty;

        [JsonPropertyName("deadline")]
        public string Deadline { get; set; } = string.Empty;

        [JsonPropertyName("risk")]
        public string Risk { get; set; } = string.Empty;

        [JsonIgnore]
        public EmailMessage OriginalMessage { get; set; } = null!;
    }
}
