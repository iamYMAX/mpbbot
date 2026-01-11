using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services;

public class EmailCacheService
{
    private readonly string _cacheFilePath = "email_cache.json";
    private readonly Dictionary<string, EmailAnalysisResult> _emailCache = new();

    public EmailCacheService()
    {
        LoadCache();
    }

    public void AddEmail(EmailAnalysisResult email)
    {
        var messageId = email.OriginalMessage.MessageId;
        if (!string.IsNullOrEmpty(messageId))
        {
            _emailCache[messageId] = email;
            SaveCache();
        }
    }

    public EmailAnalysisResult GetEmail(string messageId)
    {
        _emailCache.TryGetValue(messageId, out var email);
        return email;
    }

    public IEnumerable<EmailAnalysisResult> GetEmails()
    {
        return _emailCache.Values.OrderByDescending(e => e.OriginalMessage.Date);
    }

    public void RemoveEmail(string messageId)
    {
        if (_emailCache.Remove(messageId))
        {
            SaveCache();
        }
    }

    private void SaveCache()
    {
        var json = JsonSerializer.Serialize(_emailCache);
        File.WriteAllText(_cacheFilePath, json);
    }

    private void LoadCache()
    {
        if (File.Exists(_cacheFilePath))
        {
            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<Dictionary<string, EmailAnalysisResult>>(json);
            if (cache != null)
            {
                _emailCache = cache;
            }
        }
    }
}
