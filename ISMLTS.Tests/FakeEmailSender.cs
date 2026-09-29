using ISMLTS_WebApp_.Services;

namespace ISMLTS.Tests
{
    public sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Body)> Sent { get; } = new();

        public bool IsEnabled => true;

        public Task SendAsync(string to, string subject, string plainText)
        {
            Sent.Add((to, subject, plainText));
            return Task.CompletedTask;
        }
    }
}
