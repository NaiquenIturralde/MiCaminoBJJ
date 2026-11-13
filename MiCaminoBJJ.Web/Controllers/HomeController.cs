using MiCaminoBJJ.Web.Models;
using MiCaminoBJJ.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MiCaminoBJJ.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHomeContentService _homeContent;

        public HomeController(IHomeContentService homeContent)
        {
            _homeContent = homeContent;
        }

        public IActionResult Index()
        {
            return View(_homeContent.GetLandingContent());
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
