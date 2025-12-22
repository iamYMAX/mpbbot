using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using TelegramGigaChatBot.Configuration;

namespace TelegramGigaChatBot.Services
{
    public class YandexSttService
    {
        private readonly YandexSettings _settings;
        private static readonly HttpClient _httpClient = new HttpClient();
        private static string? _iamToken;
        private static DateTime _tokenExpiry;
        private static readonly SemaphoreSlim _tokenSemaphore = new SemaphoreSlim(1, 1);

        public YandexSttService(YandexSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(_settings.FolderId) || string.IsNullOrEmpty(_settings.ServiceAccountKeyPath) || string.IsNullOrEmpty(_settings.IamTokenUrl) || string.IsNullOrEmpty(_settings.SttUrl))
            {
                throw new InvalidOperationException("One or more YandexSpeechKit settings are not configured.");
            }
        }

        private async Task EnsureValidTokenAsync(CancellationToken cancellationToken)
        {
            await _tokenSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!string.IsNullOrEmpty(_iamToken) && DateTime.UtcNow < _tokenExpiry)
                {
                    return;
                }

                var jwt = CreateJwt();
                var requestBody = new { jwt };
                var response = await _httpClient.PostAsJsonAsync(_settings.IamTokenUrl, requestBody, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"Yandex IAM Token Error: {response.StatusCode}\n{errorBody}");
                    throw new HttpRequestException("Could not retrieve Yandex IAM token. Check service account key and configuration.");
                }

                var tokenResponse = await response.Content.ReadFromJsonAsync<YandexIamTokenResponse>(cancellationToken: cancellationToken);
                if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.IamToken))
                    throw new InvalidOperationException("Failed to deserialize or parse IAM token from Yandex response.");

                _iamToken = tokenResponse.IamToken;
                // Cache token for 11 hours (Yandex tokens are valid for 12)
                _tokenExpiry = DateTime.UtcNow.AddHours(11);
            }
            finally
            {
                _tokenSemaphore.Release();
            }
        }

        private string CreateJwt()
        {
            if (!File.Exists(_settings.ServiceAccountKeyPath))
            {
                throw new FileNotFoundException("Service account key file not found.", _settings.ServiceAccountKeyPath);
            }

            var keyJson = File.ReadAllText(_settings.ServiceAccountKeyPath);
            var keyData = JsonSerializer.Deserialize<ServiceAccountKey>(keyJson);

            if (keyData == null) throw new InvalidOperationException("Failed to deserialize service account key.");

            var now = DateTime.UtcNow;

            var privateKeyPem = keyData.PrivateKey!;

            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKeyPem);

            var securityKey = new RsaSecurityKey(rsa) { KeyId = keyData.Id };
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSsaPssSha256);
            
            var handler = new JwtSecurityTokenHandler();
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = keyData.ServiceAccountId,
                Audience = _settings.IamTokenUrl,
                SigningCredentials = signingCredentials,
                NotBefore = now,
                Expires = now.AddHours(1),
                IssuedAt = now
            };
            
            var jwtToken = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(jwtToken);
        }

        public async Task<(bool success, string text)> RecognizeSpeechAsync(Stream audioStream, CancellationToken cancellationToken)
        {
            try
            {
                await EnsureValidTokenAsync(cancellationToken);

                var requestUrl = $"{_settings.SttUrl}?folderId={_settings.FolderId}&lang=ru-RU&format=oggopus&profanityFilter=false";
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StreamContent(audioStream)
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _iamToken);

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    Console.WriteLine($"Yandex STT API Error: {response.StatusCode}\n{errorBody}");
                    return (false, "Сервис распознавания временно недоступен. Проверьте конфигурацию.");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync(cancellationToken);
                var sttResponse = JsonSerializer.Deserialize<YandexSttResponse>(jsonResponse);

                var result = sttResponse?.Result;
                if (string.IsNullOrEmpty(result))
                {
                    return (false, "Не удалось распознать речь. Попробуйте говорить чуть медленнее.");
                }
                
                return (true, result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Yandex STT Error: {ex.Message}");
                return (false, "Произошла внутренняя ошибка при распознавании речи.");
            }
        }

        private class YandexIamTokenResponse
        {
            [JsonPropertyName("iamToken")]
            public string? IamToken { get; set; }
        }

        private class YandexSttResponse
        {
            [JsonPropertyName("result")]
            public string? Result { get; set; }
        }

        private class ServiceAccountKey
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
            [JsonPropertyName("service_account_id")]
            public string? ServiceAccountId { get; set; }
            [JsonPropertyName("private_key")]
            public string? PrivateKey { get; set; }
        }
    }
}
