using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services;

public static class MemoryService
{
    private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "memory.json");
    private static Dictionary<string, UserMemory> _userMemories = new();
    private static readonly object FileLock = new();
    private const int MaxMemorySize = 10;

    static MemoryService()
    {
        LoadMemory();
    }
    
    private static UserMemory GetOrCreateUserMemory(string userId)
    {
        if (!_userMemories.ContainsKey(userId))
        {
            _userMemories[userId] = new UserMemory();
        }
        return _userMemories[userId];
    }

    private static void LoadMemory()
    {
        lock (FileLock)
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    var json = System.IO.File.ReadAllText(FilePath);
                    _userMemories = JsonSerializer.Deserialize<Dictionary<string, UserMemory>>(json) ?? new Dictionary<string, UserMemory>();
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                    SaveChanges();
                }
            }
            catch
            {
                _userMemories = new Dictionary<string, UserMemory>();
            }
        }
    }

    private static void SaveChanges()
    {
        lock (FileLock)
        {
            var options = new JsonSerializerOptions { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
            var json = JsonSerializer.Serialize(_userMemories, options);
            System.IO.File.WriteAllText(FilePath, json);
        }
    }

    public static void AddMessage(string userId, string message)
    {
        lock (FileLock)
        {
            var userMemory = GetOrCreateUserMemory(userId);
            userMemory.RecentMessages.Add(message);

            if (userMemory.RecentMessages.Count > MaxMemorySize)
            {
                userMemory.RecentMessages.RemoveAt(0);
            }
            SaveChanges();
        }
    }
    
    public static List<string> GetHistory(string userId)
    {
        lock (FileLock)
        {
            return _userMemories.TryGetValue(userId, out var memory) ? memory.RecentMessages.ToList() : new List<string>();
        }
    }

    public static void SetMode(string userId, ThinkingMode mode)
    {
        lock (FileLock)
        {
            var userMemory = GetOrCreateUserMemory(userId);
            userMemory.Mode = mode.ToString();
            SaveChanges();
        }
    }

    public static ThinkingMode GetMode(string userId)
    {
        lock (FileLock)
        {
            if (_userMemories.TryGetValue(userId, out var memory) && Enum.TryParse<ThinkingMode>(memory.Mode, true, out var mode))
            {
                return mode;
            }
            return ThinkingMode.Rational; // Default mode
        }
    }

    public static void SetLastGigaChatResponse(string userId, string response)
    {
        lock (FileLock)
        {
            var userMemory = GetOrCreateUserMemory(userId);
            userMemory.LastGigaChatResponse = response;
            SaveChanges();
        }
    }

    public static string? GetLastGigaChatResponse(string userId)
    {
        lock (FileLock)
        {
            _userMemories.TryGetValue(userId, out var memory);
            return memory?.LastGigaChatResponse;
        }
    }
    
    public static void SetLastSituation(string userId, string situation)
    {
        lock (FileLock)
        {
            var userMemory = GetOrCreateUserMemory(userId);
            userMemory.LastSituation = situation;
            SaveChanges();
        }
    }

    public static string? GetLastSituation(string userId)
    {
        lock (FileLock)
        {
            _userMemories.TryGetValue(userId, out var memory);
            return memory?.LastSituation;
        }
    }

    public static void ClearContext(string userId)
    {
        lock (FileLock)
        {
            if (_userMemories.ContainsKey(userId))
            {
                _userMemories.Remove(userId);
                SaveChanges();
            }
        }
    }
}
