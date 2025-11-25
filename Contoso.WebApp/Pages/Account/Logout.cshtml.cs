using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class LogoutModel : PageModel
{

    public IActionResult OnGet()
    {
        return Redirect("/auth/logout");
    }
}