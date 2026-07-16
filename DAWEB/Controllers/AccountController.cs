using DAWEB.Models.ViewModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DAWEB.Services.Email; // Thêm namespace EmailService

namespace DAWEB.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly EmailService _emailService; // Khai báo dịch vụ gửi mail

        // Cập nhật Constructor để nhận EmailService
        public AccountController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Đăng nhập thành công";
                return RedirectToAction("Index", "Home");
            }
            
            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "Tài khoản của bạn đã bị khóa.");
                return View(model);
            }

            ModelState.AddModelError("", "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = new IdentityUser { UserName = model.Email, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "User");
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["SuccessMessage"] = "Đăng ký thành công";
                return RedirectToAction("Index", "Home");
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "Bạn đã đăng xuất thành công!";
            return RedirectToAction("Index", "Home");
        }

        // ==========================================
        // CHỨC NĂNG QUÊN MẬT KHẨU (MỚI THÊM)
        // ==========================================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("", "Vui lòng nhập Email.");
                return View();
            }

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "Email không tồn tại trong hệ thống SweetBite.");
                return View();
            }

            // Tạo mật khẩu ngẫu nhiên tuân thủ theo IdentityOptions (Có Hoa, thường, số, ký tự đặc biệt)
            string newPassword = "Bite@" + Guid.NewGuid().ToString().Substring(0, 6) + "1";

            // Tạo token reset và cập nhật mật khẩu mới cho user
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                string subject = "🎂 Cấp Lại Mật Khẩu Đăng Nhập - SweetBite";
                string bodyHtml = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #f2e6d9; border-radius: 12px;'>
                        <div style='text-align: center;'>
                            <h2 style='color: #2c1c13; margin-bottom: 5px;'>YÊU CẦU CẤP LẠI MẬT KHẨU</h2>
                            <p style='color: #718096;'>Chào mừng quay lại với SweetBite</p>
                        </div>
                        <hr style='border: none; border-top: 2px solid #be9355; margin: 20px 0;' />
                        <p>Hệ thống vừa tạo một mật khẩu mới ngẫu nhiên cho tài khoản <strong>{user.Email}</strong> của bạn.</p>
                        
                        <div style='background-color: #fcf8f2; padding: 15px; border-radius: 8px; text-align: center; margin: 20px 0;'>
                            <p style='margin: 0; font-size: 0.9em; color: #2c1c13;'>Mật khẩu tạm thời:</p>
                            <p style='margin: 5px 0 0 0; font-size: 1.4em; font-weight: bold; color: #be9355; letter-spacing: 1px;'>{newPassword}</p>
                        </div>
                        
                        <p style='font-size: 0.9em; color: #e53e3e;'>⚠️ <strong>Lưu ý bảo mật:</strong> Vui lòng dùng mật khẩu này đăng nhập ngay và tiến hành thay đổi mật khẩu mới trong trang quản lý cá nhân để tăng tính bảo mật.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                        <p style='text-align: center; font-size: 0.85em; color: #a0aec0;'>Cảm ơn bạn đã lựa chọn tiệm bánh của chúng tôi! Chúc bạn một ngày ngọt ngào!</p>
                    </div>";

                await _emailService.SendEmailAsync(email, subject, bodyHtml);

                TempData["SuccessMessage"] = "Mật khẩu mới đã được gửi về Email của bạn thành công!";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }
            return View();
        }
    }
}