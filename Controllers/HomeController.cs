using Microsoft.AspNetCore.Mvc;

namespace ProjetoPOO.Controllers;

public class HomeController : Controller
{
    [HttpGet]
    public IActionResult Error()
    {
        return View("Error");
    }
}
