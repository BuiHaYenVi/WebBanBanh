using DAWEB.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using DAWEB.Models.ViewModel;
using Newtonsoft.Json;

namespace DAWEB.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public HomeController(ILogger<HomeController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public IActionResult Index()
        {
            var configDir = Path.Combine(_webHostEnvironment.WebRootPath, "config");
            var configFilePath = Path.Combine(configDir, "homepage.json");
            var config = new HomePageConfig();
            
            if (System.IO.File.Exists(configFilePath))
            {
                var json = System.IO.File.ReadAllText(configFilePath);
                config = JsonConvert.DeserializeObject<HomePageConfig>(json) ?? new HomePageConfig();
            }

            ViewBag.HomePageConfig = config;

            return View();
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
