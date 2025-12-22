using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using TelegramGigaChatBot.Configuration;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services
{
    public class MailReaderService
    {
        private readonly EmailSettings _emailSettings;

        public MailReaderService(EmailSettings emailSettings)
        {
            _emailSettings = emailSettings;
        }

        public async Task<List<EmailMessage>> GetUnreadEmailsAsync(CancellationToken cancellationToken)
        {
            var allUnreadEmails = new List<EmailMessage>();

            foreach (var account in _emailSettings.Accounts)
            {
                try
                {
                    using var client = new ImapClient();
                    await client.ConnectAsync(account.ImapHost, account.ImapPort, account.ImapUseSsl, cancellationToken);
                    
                    var password = SecurityService.Decrypt(account.Password, _emailSettings.EncryptionKey);
                    await client.AuthenticateAsync(account.Login, password, cancellationToken);

                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                    var uids = await inbox.SearchAsync(SearchQuery.NotSeen, cancellationToken);
                    foreach (var uid in uids)
                    {
                        var message = await inbox.GetMessageAsync(uid, cancellationToken);
                        allUnreadEmails.Add(new EmailMessage
                        {
                            From = message.From.ToString(),
                            Subject = message.Subject,
                            Body = message.TextBody ?? string.Empty,
                            Date = message.Date.DateTime,
                            Account = account
                        });
                    }

                    await client.DisconnectAsync(true, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to read emails for {account.EmailAddress}: {ex.Message}");
                    // Continue to the next account
                }
            }

            return allUnreadEmails;
        }
    }
}
