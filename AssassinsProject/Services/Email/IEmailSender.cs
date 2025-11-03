using System.Threading;
using System.Threading.Tasks;

namespace AssassinsProject.Services.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    }
}
