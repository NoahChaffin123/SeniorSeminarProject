using System;
using System.Net;
using System.Text;
using AssassinsProject.Models;

namespace AssassinsProject.Services.Email
{
    public static class AssignmentEmailBuilder
    {
        private static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        private static string? MakeAbsoluteUrl(string baseUrl, string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            raw = raw.Trim();

            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                if (!uri.Host.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                    return raw;

                var pathAndQuery = uri.PathAndQuery.TrimStart('/');
                return $"{baseUrl.TrimEnd('/')}/{pathAndQuery}";
            }

            return $"{baseUrl.TrimEnd('/')}/{raw.TrimStart('/')}";
        }

        public static (string Subject, string HtmlBody) BuildGameStartEmail(
            Game game,
            Player me,
            Player? target,
            string baseUrl,
            string reportUrl)
        {
            var gameName = game?.Name ?? "Assassins";
            var subject = $"The game \"{gameName}\" has started!";

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
                    absolutePhotoUrl = MakeAbsoluteUrl(baseUrl, target.PhotoUrl);
                    details.AppendLine(
                        $"  <li><strong>Photo:</strong> <a href=\"{H(absolutePhotoUrl)}\">{H(absolutePhotoUrl)}</a></li>");
                }
            }

            details.AppendLine("</ul>");

            var html = new StringBuilder()
                .AppendLine($"<h2>The game \"{H(gameName)}\" has started!</h2>")
                .AppendLine("<p><strong>Your passcode:</strong> " +
                            H(me.PasscodePlaintext ?? "(not set)") + "</p>")
                .AppendLine("<p>You can report eliminations here: " +
                            $"<a href=\"{H(reportUrl)}\">{H(reportUrl)}</a></p>")
                .AppendLine("<p><strong>Your current target:</strong></p>")
                .AppendLine(details.ToString());

            if (!string.IsNullOrWhiteSpace(absolutePhotoUrl))
            {
                html.AppendLine("<div style=\"margin:12px 0;\">")
                    .AppendLine($"  <img src=\"{H(absolutePhotoUrl)}\" alt=\"Target photo\"")
                    .AppendLine(
                        "       style=\"max-width:480px;width:100%;height:auto;border-radius:8px;border:1px solid #ddd;display:block;\" />")
                    .AppendLine("</div>");
            }

            html.AppendLine(
                "<p><em>Do not share your passcode. You’ll need it when reporting or confirming eliminations.</em></p>");

            return (subject, html.ToString());
        }

        public static (string Subject, string HtmlBody) BuildReassignmentEmail(
            Game game,
            Player me,
            Player? newTarget,
            string baseUrl,
            string previousTargetName,
            string reportUrl)
        {
            var gameName = game?.Name ?? "Assassins";
            var subject = $"{gameName} Target Reassignment";

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
                    absolutePhotoUrl = MakeAbsoluteUrl(baseUrl, newTarget.PhotoUrl);
                    details.AppendLine(
                        $"  <li><strong>Photo:</strong> <a href=\"{H(absolutePhotoUrl)}\">{H(absolutePhotoUrl)}</a></li>");
                }
            }

            details.AppendLine("</ul>");

            var html = new StringBuilder()
                .AppendLine("<h2>You have been assigned a new target due to them being removed</h2>")
                .AppendLine("<p>")
                .Append("The target you were previously after, ")
                .Append("<strong>").Append(H(previousTargetName)).Append("</strong>, ")
                .AppendLine("has been removed from the game by the administrator.")
                .AppendLine("</p>")
                .AppendLine("<p><strong>Your passcode:</strong> " +
                            H(me.PasscodePlaintext ?? "(not set)") + "</p>")
                .AppendLine("<p>You can report eliminations here: " +
                            $"<a href=\"{H(reportUrl)}\">{H(reportUrl)}</a></p>")
                .AppendLine("<p><strong>Your new target:</strong></p>")
                .AppendLine(details.ToString());

            if (!string.IsNullOrWhiteSpace(absolutePhotoUrl))
            {
                html.AppendLine("<div style=\"margin:12px 0;\">")
                    .AppendLine($"  <img src=\"{H(absolutePhotoUrl)}\" alt=\"Target photo\"")
                    .AppendLine(
                        "       style=\"max-width:480px;width:100%;height:auto;border-radius:8px;border:1px solid #ddd;display:block;\" />")
                    .AppendLine("</div>");
            }

            html.AppendLine(
                "<p><em>Do not share your passcode. You’ll need it when reporting or confirming eliminations.</em></p>");

            return (subject, html.ToString());
        }
    }
}
