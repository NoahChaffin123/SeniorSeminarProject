using System.Text;
using AssassinsProject.Models;

namespace AssassinsProject.Services.Email
{
    public static class AssignmentEmailBuilder
    {
        public sealed record Result(string Subject, string TextBody, string HtmlBody);

        public static Result Build(Game game, Player recipient, Player? target, string? baseUrl = null)
        {
            var subject = $"{game.Name} Target and Password";

            // Build the plain-text body
            var txt = new StringBuilder();
            txt.AppendLine($"The game \"{game.Name}\" has started!");
            txt.AppendLine();
            txt.AppendLine($"Your passcode: {recipient.PasscodePlaintext}");
            txt.AppendLine();
            txt.AppendLine("Your current target:");
            if (target == null)
            {
                txt.AppendLine("(No target assigned yet.)");
            }
            else
            {
                txt.AppendLine($"• Alias: {target.Alias}");
                txt.AppendLine($"• Display Name: {target.DisplayName}");
                if (target.ApproximateAge.HasValue)
                    txt.AppendLine($"• Approximate Age: {target.ApproximateAge}");
                if (!string.IsNullOrWhiteSpace(target.HairColor))
                    txt.AppendLine($"• Hair Color: {target.HairColor}");
                if (!string.IsNullOrWhiteSpace(target.EyeColor))
                    txt.AppendLine($"• Eye Color: {target.EyeColor}");
                if (!string.IsNullOrWhiteSpace(target.VisibleMarkings))
                    txt.AppendLine($"• Visible Markings: {target.VisibleMarkings}");
                if (!string.IsNullOrWhiteSpace(target.Specialty))
                    txt.AppendLine($"• Specialty: {target.Specialty}");
                if (!string.IsNullOrWhiteSpace(target.PhotoUrl))
                {
                    string photoUrl = target.PhotoUrl;
                    if (!string.IsNullOrEmpty(baseUrl) && !photoUrl.StartsWith("http"))
                        photoUrl = $"{baseUrl.TrimEnd('/')}/{photoUrl.TrimStart('/')}";
                    txt.AppendLine($"• Photo: {photoUrl}");
                }
            }
            txt.AppendLine();
            txt.AppendLine("Remember: never share your passcode. You'll need it when reporting an elimination.");

            // Build HTML body
            string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var html = new StringBuilder();
            html.AppendLine($"<h2>The game \"{H(game.Name)}\" has started!</h2>");
            html.AppendLine($"<p><strong>Your passcode:</strong> {H(recipient.PasscodePlaintext)}</p>");
            html.AppendLine("<h3>Your current target:</h3>");
            html.AppendLine("<ul>");
            if (target == null)
            {
                html.AppendLine("<li>(No target assigned yet.)</li>");
            }
            else
            {
                html.AppendLine($"<li><strong>Alias:</strong> {H(target.Alias)}</li>");
                html.AppendLine($"<li><strong>Display Name:</strong> {H(target.DisplayName)}</li>");
                if (target.ApproximateAge.HasValue)
                    html.AppendLine($"<li><strong>Approximate Age:</strong> {target.ApproximateAge}</li>");
                if (!string.IsNullOrWhiteSpace(target.HairColor))
                    html.AppendLine($"<li><strong>Hair Color:</strong> {H(target.HairColor)}</li>");
                if (!string.IsNullOrWhiteSpace(target.EyeColor))
                    html.AppendLine($"<li><strong>Eye Color:</strong> {H(target.EyeColor)}</li>");
                if (!string.IsNullOrWhiteSpace(target.VisibleMarkings))
                    html.AppendLine($"<li><strong>Visible Markings:</strong> {H(target.VisibleMarkings)}</li>");
                if (!string.IsNullOrWhiteSpace(target.Specialty))
                    html.AppendLine($"<li><strong>Specialty:</strong> {H(target.Specialty)}</li>");
                if (!string.IsNullOrWhiteSpace(target.PhotoUrl))
                {
                    string photoUrl = target.PhotoUrl;
                    if (!string.IsNullOrEmpty(baseUrl) && !photoUrl.StartsWith("http"))
                        photoUrl = $"{baseUrl.TrimEnd('/')}/{photoUrl.TrimStart('/')}";
                    html.AppendLine($"<li><strong>Photo:</strong> <a href=\"{H(photoUrl)}\">{H(photoUrl)}</a></li>");
                    html.AppendLine($"</ul><p><img src=\"{H(photoUrl)}\" alt=\"Target photo\" style=\"max-width:300px;border-radius:10px;\"/></p><ul>");
                }
            }
            html.AppendLine("</ul>");
            html.AppendLine("<p><em>Do not share your passcode. You'll need it when reporting an elimination.</em></p>");

            return new Result(subject, txt.ToString(), html.ToString());
        }
    }
}
