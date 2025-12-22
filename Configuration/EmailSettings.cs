using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TelegramGigaChatBot.Configuration
{
    public class EmailSettings
    {
        [JsonPropertyName("EncryptionKey")]
        public string EncryptionKey { get; set; } = string.Empty;

        [JsonPropertyName("Accounts")]
        public List<EmailAccount> Accounts { get; set; } = new();
    }

    public class EmailAccount
    {
        [JsonPropertyName("EmailAddress")]
        public string EmailAddress { get; set; } = string.Empty;

        [JsonPropertyName("ImapHost")]
        public string ImapHost { get; set; } = string.Empty;

        [JsonPropertyName("ImapPort")]
        public int ImapPort { get; set; }

        [JsonPropertyName("ImapUseSsl")]
        public bool ImapUseSsl { get; set; }

        [JsonPropertyName("SmtpHost")]
        public string SmtpHost { get; set; } = string.Empty;

        [JsonPropertyName("SmtpPort")]
        public int SmtpPort { get; set; }

        [JsonPropertyName("SmtpUseSsl")]
        public bool SmtpUseSsl { get; set; }

        [JsonPropertyName("Login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("Password")]
        public string Password { get; set; } = string.Empty;
    }
}
