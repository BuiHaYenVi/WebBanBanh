using DAWEB.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.IO;
using System.Threading.Tasks;

namespace DAWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly string _configFilePath;

        public HomeController(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
            // Đảm bảo thư mục config tồn tại
            var configDir = Path.Combine(_webHostEnvironment.WebRootPath, "config");
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            _configFilePath = Path.Combine(configDir, "homepage.json");
        }

        [HttpGet]
        public IActionResult Index()
        {
            var config = GetHomePageConfig();
            return View(config);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(HomePageConfig model, IFormFile? backgroundFile)
        {
            var currentConfig = GetHomePageConfig();

            // Nếu người dùng tải lên ảnh mới
            if (backgroundFile != null && backgroundFile.Length > 0)
            {
                // Thư mục lưu ảnh media
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "media");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Tạo tên file duy nhất tránh trùng lặp
                var uniqueFileName = System.Guid.NewGuid().ToString() + "_" + backgroundFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await backgroundFile.CopyToAsync(fileStream);
                }

                // Cập nhật đường dẫn ảnh
                model.BackgroundImage = "/media/" + uniqueFileName;
            }
            else
            {
                // Giữ lại ảnh cũ nếu không up ảnh mới
                model.BackgroundImage = currentConfig.BackgroundImage;
            }

            // Lưu dữ liệu vào file JSON
            var jsonString = JsonConvert.SerializeObject(model, Formatting.Indented);
            System.IO.File.WriteAllText(_configFilePath, jsonString);

            TempData["SuccessMessage"] = "Cập nhật giao diện Trang chủ thành công!";
            return RedirectToAction("Index");
        }

        private HomePageConfig GetHomePageConfig()
        {
            if (System.IO.File.Exists(_configFilePath))
            {
                var json = System.IO.File.ReadAllText(_configFilePath);
                return JsonConvert.DeserializeObject<HomePageConfig>(json) ?? new HomePageConfig();
            }

            return new HomePageConfig(); // Trả về cấu hình mặc định (trong HomePageConfig.cs)
        }
    }
}
