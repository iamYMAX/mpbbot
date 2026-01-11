using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TelegramGigaChatBot.Configuration;

namespace TelegramGigaChatBot.Services
{
    public class UserEmailAccount
    {
        public long UserId { get; set; }
        public EmailAccount Account { get; set; }
    }

    public class UserEmailAccountService
    {
        private readonly string _filePath = "user_emails.json";
        private List<UserEmailAccount> _accounts = new();
        private readonly string _encryptionKey;
        private readonly object _lock = new object();

        public UserEmailAccountService(AppSettings settings)
        {
            _encryptionKey = settings.EmailSettings.EncryptionKey;
            LoadAccounts();
        }

        public void AddAccount(long userId, EmailAccount account)
        {
            account.Password = SecurityService.Encrypt(account.Password, _encryptionKey);
            _accounts.Add(new UserEmailAccount { UserId = userId, Account = account });
            SaveChanges();
        }

        public List<EmailAccount> GetAccounts(long userId)
        {
            return _accounts.Where(a => a.UserId == userId)
                            .Select(a => {
                                var decryptedAccount = a.Account;
                                decryptedAccount.Password = SecurityService.Decrypt(decryptedAccount.Password, _encryptionKey);
                                return decryptedAccount;
                            }).ToList();
        }

        public IEnumerable<long> GetAllUsers()
        {
            return _accounts.Select(a => a.UserId).Distinct();
        }

        private void LoadAccounts()
        {
            lock (_lock)
            {
                if (File.Exists(_filePath))
                {
                    var json = File.ReadAllText(_filePath);
                    var accounts = JsonSerializer.Deserialize<List<UserEmailAccount>>(json);
                    if (accounts != null)
                    {
                        _accounts = accounts;
                    }
                }
            }
        }

        private void SaveChanges()
        {
            lock (_lock)
            {
                var json = JsonSerializer.Serialize(_accounts);
                File.WriteAllText(_filePath, json);
            }
        }
    }
}
