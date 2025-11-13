using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Auth
{
    public class LoginModel : PageModel
    {
        private readonly AdminGuard _guard;

        public LoginModel(AdminGuard guard)
        {
            _guard = guard;
        }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        [BindProperty]
        public string? Passcode { get; set; }

        public string? ErrorMessage { get; set; }

        public void OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;
            if (_guard.IsAdmin(HttpContext) && !string.IsNullOrWhiteSpace(ReturnUrl))
            {
                Response.Redirect(ReturnUrl);
            }
        }

        public IActionResult OnPost(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (_guard.TrySignIn(HttpContext, Passcode))
            {
                if (!string.IsNullOrWhiteSpace(ReturnUrl))
                    return Redirect(ReturnUrl);

                return RedirectToPage("/Games/Index");
            }

            ErrorMessage = "Invalid passcode.";
            return Page();
        }
    }
}
