using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TelegramGigaChatBot.Services;

public class BackgroundEmailService : IHostedService, IDisposable
{
    private readonly ILogger<BackgroundEmailService> _logger;
    private readonly MailReaderService _mailReaderService;
    private readonly MailAnalyzerService _mailAnalyzerService;
    private readonly EmailCacheService _emailCacheService;
    private readonly UserEmailAccountService _userEmailAccountService;
    private Timer _timer;

    public BackgroundEmailService(
        ILogger<BackgroundEmailService> logger,
        MailReaderService mailReaderService,
        MailAnalyzerService mailAnalyzerService,
        EmailCacheService emailCacheService,
        UserEmailAccountService userEmailAccountService)
    {
        _logger = logger;
        _mailReaderService = mailReaderService;
        _mailAnalyzerService = mailAnalyzerService;
        _emailCacheService = emailCacheService;
        _userEmailAccountService = userEmailAccountService;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background Email Service is starting.");
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    private async void DoWork(object state)
    {
        _logger.LogInformation("Background Email Service is working.");
        try
        {
            // This is a simplistic approach. In a real application, you'd want to manage users more dynamically.
            var allUserIds = _userEmailAccountService.GetAllUsers();

            foreach (var userId in allUserIds)
            {
                var unreadEmails = await _mailReaderService.GetUnreadEmailsAsync(userId, CancellationToken.None);
                foreach (var email in unreadEmails)
                {
                    var analyzedEmail = await _mailAnalyzerService.AnalyzeEmailAsync(email, CancellationToken.None);
                    _emailCacheService.AddEmail(analyzedEmail);
                    _logger.LogInformation($"New email for user {userId} from {email.From} with subject '{email.Subject}' was received and analyzed.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while checking for new emails.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Background Email Service is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
