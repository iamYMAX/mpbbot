using System.Collections.Generic;

namespace TelegramGigaChatBot.Models;

public class UserMemory
{
    public List<string> RecentMessages { get; set; } = new();
    public string Mode { get; set; } = "rational";
    public string? LastGigaChatResponse { get; set; }
    public string? LastSituation { get; set; }
}
