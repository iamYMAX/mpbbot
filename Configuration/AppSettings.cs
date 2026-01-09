namespace TelegramGigaChatBot.Configuration;

public class AppSettings
{
    public TelegramSettings Telegram { get; set; } = new();
    public GigaChatSettings GigaChat { get; set; } = new();
    public YandexSettings YandexSpeechKit { get; set; } = new();
    public EmailSettings EmailSettings { get; set; } = new();
    public BackgroundServiceSettings BackgroundService { get; set; } = new();
}
