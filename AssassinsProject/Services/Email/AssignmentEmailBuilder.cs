using System.Net;
using System.Text;
using AssassinsProject.Models;

namespace AssassinsProject.Services
{
    public static class AssignmentEmailBuilder
    {
        public sealed record EmailContent(string Subject, string HtmlBody);

        public static EmailContent Build(Game game, Player me, Player? target, string baseUrl)
        {
            static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

            var gameName = game?.Name ?? "Assassins";
            var subject  = $"The game \"{gameName}\" has started!";

            var tAlias = target?.Alias ?? "(no target assigned yet)";
            var tDisplay = string.IsNullOrWhiteSpace(target?.DisplayName)
                ? target?.Alias
                : target?.DisplayName;

            var details = new StringBuilder()
                .AppendLine("<ul>")
                .AppendLine($"  <li><strong>Alias:</strong> {H(tAlias)}</li>")
                .AppendLine($"  <li><strong>Display Name:</strong> {H(tDisplay)}</li>");

            string? absolutePhotoUrl = null;

            if (target is not null)
            {
                if (target.ApproximateAge.HasValue)
                    details.AppendLine($"  <li><strong>Approximate Age:</strong> {target.ApproximateAge.Value}</li>");
                if (!string.IsNullOrWhiteSpace(target.HairColor))
                    details.AppendLine($"  <li><strong>Hair Color:</strong> {H(target.HairColor)}</li>");
                if (!string.IsNullOrWhiteSpace(target.EyeColor))
                    details.AppendLine($"  <li><strong>Eye Color:</strong> {H(target.EyeColor)}</li>");
                if (!string.IsNullOrWhiteSpace(target.VisibleMarkings))
                    details.AppendLine($"  <li><strong>Visible Markings:</strong> {H(target.VisibleMarkings)}</li>");
                if (!string.IsNullOrWhiteSpace(target.Specialty))
                    details.AppendLine($"  <li><strong>Specialty:</strong> {H(target.Specialty)}</li>");

                if (!string.IsNullOrWhiteSpace(target.PhotoUrl))
                {
                    // For photos, still respect the baseUrl coming from config
                    absolutePhotoUrl = target.PhotoUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? target.PhotoUrl!
                        : $"{baseUrl.TrimEnd('/')}/{target.PhotoUrl!.TrimStart('/')}";

                    details.AppendLine(
                        $"  <li><strong>Photo:</strong> <a href=\"{H(absolutePhotoUrl)}\">{H(absolutePhotoUrl)}</a></li>");
                }
            }

            details.AppendLine("</ul>");

            // Always use the Azure base URL for the report-elimination link,
            // regardless of what baseUrl was passed in.
            const string azureBase =
                "https://assassins-game-cjddb5dydyfsb4bv.centralus-01.azurewebsites.net";

            var reportUrl        = $"{azureBase.TrimEnd('/')}/Eliminations/Report?gameId={game.Id}";
            var reportUrlEscaped = H(reportUrl);

            var htmlBuilder = new StringBuilder()
                .AppendLine($"<h2>The game \"{H(gameName)}\" has started!</h2>")
                .AppendLine("<p><strong>Your passcode:</strong> " +
                            H(me.PasscodePlaintext ?? "(not set)") + "</p>")
                .AppendLine($"<p>You can report eliminations here: <a href=\"{reportUrlEscaped}\">{reportUrlEscaped}</a></p>")
                .AppendLine("<p><strong>Your current target:</strong></p>")
                .AppendLine(details.ToString());

            if (!string.IsNullOrWhiteSpace(absolutePhotoUrl))
            {
                htmlBuilder
                    .AppendLine("<div style=\"margin:12px 0;\">")
                    .AppendLine($"  <img src=\"{H(absolutePhotoUrl)}\" alt=\"Target photo\"")
                    .AppendLine("       style=\"max-width:480px;width:100%;height:auto;border-radius:8px;border:1px solid #ddd;display:block;\" />")
                    .AppendLine("</div>");
            }

            htmlBuilder
                .AppendLine("<p><em>Do not share your passcode. You’ll need it when reporting or confirming eliminations.</em></p>");

            return new EmailContent(subject, htmlBuilder.ToString());
        }
    }
}
