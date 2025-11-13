using AssassinsProject.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AssassinsProject.Pages.Auth
{
    public class LogoutModel : PageModel
    {
        private readonly AdminGuard _guard;

        public LogoutModel(AdminGuard guard)
        {
            _guard = guard;
        }

        public void OnGet()
        {
            _guard.SignOut(HttpContext);
            // Optional: redirect somewhere if you like
            // Response.Redirect("/");
        }
    }
}
