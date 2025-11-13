using System.Net;
using System.Text;
using AssassinsProject.Models;

namespace AssassinsProject.Services
{
    public static class TargetReassignmentEmailBuilder
    {
        public sealed record EmailContent(string Subject, string HtmlBody);

        public static EmailContent Build(Game game, Player me, Player? newTarget, string baseUrl)
        {
            static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

            var gameName = game?.Name ?? "Assassins";
            var subject  = $"{gameName} Target Reassignment";

            var tAlias = newTarget?.Alias ?? "(no target assigned yet)";
            var tDisplay = string.IsNullOrWhiteSpace(newTarget?.DisplayName)
                ? newTarget?.Alias
                : newTarget?.DisplayName;

            var details = new StringBuilder()
                .AppendLine("<ul>")
                .AppendLine($"  <li><strong>Alias:</strong> {H(tAlias)}</li>")
                .AppendLine($"  <li><strong>Display Name:</strong> {H(tDisplay)}</li>");

            string? absolutePhotoUrl = null;

            if (newTarget is not null)
            {
                if (newTarget.ApproximateAge.HasValue)
                    details.AppendLine($"  <li><strong>Approximate Age:</strong> {newTarget.ApproximateAge.Value}</li>");
                if (!string.IsNullOrWhiteSpace(newTarget.HairColor))
                    details.AppendLine($"  <li><strong>Hair Color:</strong> {H(newTarget.HairColor)}</li>");
                if (!string.IsNullOrWhiteSpace(newTarget.EyeColor))
                    details.AppendLine($"  <li><strong>Eye Color:</strong> {H(newTarget.EyeColor)}</li>");
                if (!string.IsNullOrWhiteSpace(newTarget.VisibleMarkings))
                    details.AppendLine($"  <li><strong>Visible Markings:</strong> {H(newTarget.VisibleMarkings)}</li>");
                if (!string.IsNullOrWhiteSpace(newTarget.Specialty))
                    details.AppendLine($"  <li><strong>Specialty:</strong> {H(newTarget.Specialty)}</li>");

                if (!string.IsNullOrWhiteSpace(newTarget.PhotoUrl))
                {
                    absolutePhotoUrl = newTarget.PhotoUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? newTarget.PhotoUrl!
                        : $"{baseUrl.TrimEnd('/')}/{newTarget.PhotoUrl!.TrimStart('/')}";

                    details.AppendLine(
                        $"  <li><strong>Photo:</strong> <a href=\"{H(absolutePhotoUrl)}\">{H(absolutePhotoUrl)}</a></li>");
                }
            }

            details.AppendLine("</ul>");

            var reportUrl = $"{baseUrl.TrimEnd('/')}/Eliminations/Report?gameId={game.Id}";
            var reportUrlEscaped = H(reportUrl);

            var htmlBuilder = new StringBuilder()
                .AppendLine("<h2>You have been assigned a new target because your previous target was removed from the game.</h2>")
                .AppendLine("<p><strong>Your passcode:</strong> " +
                            H(me.PasscodePlaintext ?? "(not set)") + "</p>")
                .AppendLine($"<p>You can report eliminations here: <a href=\"{reportUrlEscaped}\">{reportUrlEscaped}</a></p>")
                .AppendLine("<p><strong>Your new target:</strong></p>")
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
