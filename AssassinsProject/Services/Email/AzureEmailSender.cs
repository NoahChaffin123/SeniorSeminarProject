using System;
using System.Threading;
using System.Threading.Tasks;
using Azure;                       
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;

namespace AssassinsProject.Services.Email
{
    public sealed class AzureEmailSender : IEmailSender
    {
        private readonly EmailClient _client;
        private readonly string _from;

        public AzureEmailSender(IConfiguration config)
        {
            var conn = config["Email:ConnectionString"] 
                       ?? throw new InvalidOperationException("Missing configuration: Email:ConnectionString");

            _from = config["Email:FromAddress"] 
                    ?? throw new InvalidOperationException("Missing configuration: Email:FromAddress");

            _client = new EmailClient(conn);
        }

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("Recipient address is required.", nameof(to));

            var content = new EmailContent(subject) { Html = htmlBody };
            var message = new EmailMessage(_from, to, content);

            // Wait so send failures surface immediately during the request
            await _client.SendAsync(WaitUntil.Completed, message, ct);
        }
    }
}
