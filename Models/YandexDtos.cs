using System.Text.Json.Serialization;

namespace TelegramGigaChatBot.Models;

public class ServiceAccountKey
{
    public string? id { get; set; }
    public string? service_account_id { get; set; }
    public string? private_key { get; set; }
}

public class IamTokenResponse
{
    [JsonPropertyName("iamToken")]
    public string? iamToken { get; set; }
}
