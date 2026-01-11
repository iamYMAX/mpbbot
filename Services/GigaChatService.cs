using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TelegramGigaChatBot.Configuration;
using TelegramGigaChatBot.Models;

namespace TelegramGigaChatBot.Services;

public class GigaChatService
{
    private readonly HttpClient _httpClient = new();
    private readonly GigaChatSettings _settings;
    private readonly ILogger<GigaChatService> _logger;
    private string _cachedToken;
    private DateTime _tokenExpiry = DateTime.UtcNow;

    public GigaChatService(AppSettings settings, ILogger<GigaChatService> logger)
    {
        _settings = settings.GigaChat;
        _logger = logger;
    }

    private async Task<string> GetAuthTokenAsync(CancellationToken cancellationToken)
    {
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry)
        {
            return _cachedToken;
        }
        
        var request = new HttpRequestMessage(HttpMethod.Post, "https://ngw.devices.sberbank.ru:9443/api/v2/oauth");

        var credentials = $"{_settings.ClientId}:{_settings.ClientSecret}";
        var base64Credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

        request.Headers.Add("RqUID", Guid.NewGuid().ToString());
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("scope", _settings.Scope)
        });

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GigaChat Auth Error: {StatusCode} | {ResponseBody}", response.StatusCode, responseBody);
                return null;
            }

            var authResponse = JsonSerializer.Deserialize<GigaAuthResponse>(responseBody);
            _cachedToken = authResponse?.AccessToken;
            _tokenExpiry = DateTime.UnixEpoch.AddMilliseconds(authResponse?.ExpiresAt ?? 0).AddSeconds(-60);

            return _cachedToken;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "GigaChat Auth Critical Error");
            return null;
        }
    }

    public async Task<string> GetDecisionAsync(string lastMessage, List<string> history, ThinkingMode mode, CancellationToken cancellationToken)
    {
        var token = await GetAuthTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            return "Ошибка аутентификации GigaChat. Не удалось получить токен.";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://gigachat.devices.sberbank.ru/api/v1/chat/completions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());

        var stylePrompt = ThinkingModeHelper.GetStylePrompt(mode);
        var systemPrompt = $"Ты персональный советник. Твоя задача — помогать принимать решения. Отвечай структурировано и по делу. {stylePrompt}";
        var historyPrompt = string.Join("\n", history.SkipLast(1)); // All but the last message

        var userPrompt = $"Контекст пользователя:\n{historyPrompt}\n\nПоследняя мысль:\n{lastMessage}\n\nДай рекомендацию.";

        var messages = new List<GigaChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };
        
        var chatRequest = new GigaChatRequest { Messages = messages };

        var jsonPayload = JsonSerializer.Serialize(chatRequest);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GigaChat API Error: {StatusCode} | {ResponseBody}", response.StatusCode, responseBody);
                return $"Ошибка API GigaChat: {response.StatusCode}";
            }
            
            var gigaResponse = JsonSerializer.Deserialize<GigaChatResponse>(responseBody);

            if (gigaResponse?.Error?.Message != null)
            {
                 _logger.LogError("GigaChat API Error: {ErrorMessage}", gigaResponse.Error.Message);
                 return $"Ошибка от API GigaChat: {gigaResponse.Error.Message}";
            }

            if (gigaResponse?.Choices != null && gigaResponse.Choices.Any())
            {
                return gigaResponse.Choices[0].Message?.Content?.Trim() ?? "Ответ не содержит текста.";
            }
            
            _logger.LogWarning("GigaChat returned an empty or invalid response.");
            return "Ошибка: GigaChat вернул пустой или некорректный ответ.";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error during GigaChat request");
            return $"Критическая ошибка при запросе к GigaChat: {ex.Message}";
        }
    }

    public async Task<string> GetDeepAnalysisAsync(string lastAnswer, string lastSituation, List<string> history, ThinkingMode mode, CancellationToken cancellationToken)
    {
        var token = await GetAuthTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            return "Ошибка аутентификации GigaChat. Не удалось получить токен.";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://gigachat.devices.sberbank.ru/api/v1/chat/completions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());

        var stylePrompt = ThinkingModeHelper.GetStylePrompt(mode);
        var systemPrompt = $"Ты персональный аналитик и советник. Твоя задача — углублять уже данное решение, а не повторять его. {stylePrompt}";
        var historyPrompt = string.Join("\n", history);

        var userPrompt = $"Предыдущий ответ:\n{lastAnswer}\n\nКонтекст пользователя:\n{historyPrompt}\n\nИсходная ситуация:\n{lastSituation}\n\nУглуби анализ ситуации.\n\nОбязательно:\n- выяви скрытые допущения\n- покажи вторичные и отложенные последствия\n- обозначь точки невозврата\n- задай 1–2 ключевых вопроса пользователю";

        var messages = new List<GigaChatMessage>
        {
            new() { Role = "system", Content = systemPrompt },
            new() { Role = "user", Content = userPrompt }
        };
        
        var chatRequest = new GigaChatRequest { Messages = messages };

        var jsonPayload = JsonSerializer.Serialize(chatRequest);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("GigaChat API Error: {StatusCode} | {ResponseBody}", response.StatusCode, responseBody);
                return $"Ошибка API GigaChat: {response.StatusCode}";
            }
            
            var gigaResponse = JsonSerializer.Deserialize<GigaChatResponse>(responseBody);

            if (gigaResponse?.Error?.Message != null)
            {
                _logger.LogError("GigaChat API Error: {ErrorMessage}", gigaResponse.Error.Message);
                return $"Ошибка от API GigaChat: {gigaResponse.Error.Message}";
            }

            if (gigaResponse?.Choices != null && gigaResponse.Choices.Any())
            {
                return gigaResponse.Choices[0].Message?.Content?.Trim() ?? "Ответ не содержит текста.";
            }
            
            _logger.LogWarning("GigaChat returned an empty or invalid response.");
            return "Ошибка: GigaChat вернул пустой или некорректный ответ.";
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error during GigaChat request");
            return $"Критическая ошибка при запросе к GigaChat: {ex.Message}";
        }
    }
    
    public async Task<string> GetRawGigaChatResponse(string prompt, CancellationToken cancellationToken)
    {
        var token = await GetAuthTokenAsync(cancellationToken);
        if (string.IsNullOrEmpty(token))
        {
            return "{\"error\":\"GigaChat authentication failed.\"}";
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "https://gigachat.devices.sberbank.ru/api/v1/chat/completions");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var messages = new List<GigaChatMessage>
        {
            new() { Role = "user", Content = prompt }
        };
        
        var chatRequest = new GigaChatRequest { Messages = messages, Temperature = 0.1 }; // Lower temperature for predictable JSON

        var jsonPayload = JsonSerializer.Serialize(chatRequest);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            var response = await HttpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return $"{{\"error\":\"GigaChat API Error: {response.StatusCode}\", \"details\":\"{responseBody}\"}}";
            }
            
            var gigaResponse = JsonSerializer.Deserialize<GigaChatResponse>(responseBody);
            
            if (gigaResponse?.Choices != null && gigaResponse.Choices.Any())
            {
                return gigaResponse.Choices[0].Message?.Content?.Trim() ?? "{}";
            }
            
            return "{}";
        }
        catch (Exception ex)
        {
            return $"{{\"error\":\"Critical error during GigaChat request: {ex.Message}\"}}";
        }
    }
}
