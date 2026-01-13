namespace TelegramGigaChatBot.Configuration;

public class GigaChatSettings
{
    public bool UseMocks { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}
