using System;
using TelegramGigaChatBot.Configuration;

namespace TelegramGigaChatBot.Models
{
    public class EmailMessage
    {
        public string MessageId { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public EmailAccount Account { get; set; } = null!;
    }
}
