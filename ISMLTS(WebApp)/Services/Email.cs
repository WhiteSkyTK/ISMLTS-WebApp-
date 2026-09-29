using Azure;
using Azure.Communication.Email;
using Microsoft.Extensions.Options;

namespace ISMLTS_WebApp_.Services
{
    // "Email" section: ConnectionString and From come from App Service settings, never the repo
    public class EmailOptions
    {
        public string? ConnectionString { get; set; }
        public string? From { get; set; }
        public string? SiteUrl { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString) && !string.IsNullOrWhiteSpace(From);

        // Turns a local path into a link people can click in their inbox
        public string Link(string path) => $"{(SiteUrl ?? string.Empty).TrimEnd('/')}{path}";
    }

    public interface IEmailSender
    {
        bool IsEnabled { get; }
        Task SendAsync(string to, string subject, string plainText);
    }

    // Used when Email isn't configured (development, tests): nothing is sent
    public sealed class NullEmailSender : IEmailSender
    {
        public bool IsEnabled => false;
        public Task SendAsync(string to, string subject, string plainText) => Task.CompletedTask;
    }

    public sealed class AcsEmailSender : IEmailSender
    {
        private readonly EmailClient _client;
        private readonly EmailOptions _options;
        private readonly ILogger<AcsEmailSender> _logger;

        public AcsEmailSender(IOptions<EmailOptions> options, ILogger<AcsEmailSender> logger)
        {
            _options = options.Value;
            _client = new EmailClient(_options.ConnectionString);
            _logger = logger;
        }

        public bool IsEnabled => true;

        public async Task SendAsync(string to, string subject, string plainText)
        {
            try
            {
                // WaitUntil.Started hands the message to Azure without holding up the page until delivery
                var message = new EmailMessage(_options.From, to, new EmailContent(subject) { PlainText = plainText });
                await _client.SendAsync(WaitUntil.Started, message);
            }
            catch (RequestFailedException ex)
            {
                // A failed email must never break the action that triggered it; addresses stay out of the log
                _logger.LogWarning(ex, "An email could not be handed to Azure Communication Services");
            }
        }
    }
}
