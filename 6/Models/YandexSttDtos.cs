using System.Text.Json.Serialization;

namespace TelegramGigaChatBot.Models;

public class YandexSttResponse
{
    [JsonPropertyName("result")]
    public string? result { get; set; }

    [JsonPropertyName("error_code")]
    public string? error_code { get; set; }
    
    [JsonPropertyName("error_message")]
    public string? error_message { get; set; }
}
