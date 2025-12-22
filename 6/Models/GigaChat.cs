using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TelegramGigaChatBot.Models
{
    public class GigaAuthResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_at")]
        public long ExpiresAt { get; set; }
    }

    public class GigaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "GigaChat";

        [JsonPropertyName("messages")]
        public List<GigaChatMessage> Messages { get; set; } = new();
        
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.7;
    }

    public class GigaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    public class GigaChatResponse
    {
        [JsonPropertyName("choices")]
        public List<GigaChatChoice> Choices { get; set; } = new();

        [JsonPropertyName("error")]
        public GigaChatError? Error { get; set; }
    }

    public class GigaChatChoice
    {
        [JsonPropertyName("message")]
        public GigaChatMessage? Message { get; set; }
    }

    public class GigaChatError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
