using System.Collections.Concurrent;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services
{
    public class EmailCacheService
    {
        private static readonly ConcurrentDictionary<string, AnalyzedEmail> AnalyzedEmailsCache = new();
        private static readonly ConcurrentDictionary<string, string> DraftReplies = new();

        public void Add(string messageId, AnalyzedEmail email)
        {
            AnalyzedEmailsCache[messageId] = email;
        }

        public AnalyzedEmail? Get(string messageId)
        {
            return AnalyzedEmailsCache.GetValueOrDefault(messageId);
        }

        public void Remove(string messageId)
        {
            AnalyzedEmailsCache.TryRemove(messageId, out _);
        }

        public void AddDraft(string messageId, string draft)
        {
            DraftReplies[messageId] = draft;
        }

        public string? GetDraft(string messageId)
        {
            return DraftReplies.GetValueOrDefault(messageId);
        }

        public void RemoveDraft(string messageId)
        {
            DraftReplies.TryRemove(messageId, out _);
        }
    }
}
