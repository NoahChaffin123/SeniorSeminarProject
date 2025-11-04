using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Signup
{
    public class SentModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Email { get; set; } = string.Empty;

        public void OnGet(int gameId, string email)
        {
            GameId = gameId;
            Email = email ?? string.Empty;
        }
    }
}
