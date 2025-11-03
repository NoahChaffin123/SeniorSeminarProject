using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace AssassinsProject.Pages.Signup
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        // The page / markup expects this (e.g., as a route/query like ?gameId=1)
        [BindProperty(SupportsGet = true)]
        public int GameId { get; set; }

        // Used by the Razor page to enable/disable the form
        public bool IsSignupOpen { get; private set; } = true;

        // Form fields the .cshtml binds to via asp-for
        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string RealName { get; set; } = string.Empty;
        [BindProperty] public string Alias { get; set; } = string.Empty;

        [BindProperty] public string? HairColor { get; set; }
        [BindProperty] public string? EyeColor { get; set; }
        [BindProperty] public string? VisibleMarkings { get; set; }
        [BindProperty] public int?    ApproximateAge { get; set; }
        [BindProperty] public string? Specialty { get; set; }

        [BindProperty] public IFormFile? Photo { get; set; }

        public void OnGet()
        {
            // If you manage game signup windows in DB, set IsSignupOpen accordingly here
            // using GameId to fetch game info.
            // IsSignupOpen = ...;
            _logger.LogInformation("Signup page loaded for GameId={GameId}", GameId);
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // This is a placeholder handler to satisfy current bindings.
            _logger.LogInformation("Signup POST for {Email} (GameId={GameId})", Email, GameId);
            return Page();
        }
    }
}
