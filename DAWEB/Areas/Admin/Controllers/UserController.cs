using DAWEB.Models.ViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DAWEB.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = _userManager.Users.ToList();
            var userViewModels = new List<UserWithRoleViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new UserWithRoleViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    RoleNames = string.Join(", ", roles),
                    IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.Now
                });
            }

            return View(userViewModels);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var currentUserId = _userManager.GetUserId(User); // ID của user đang đăng nhập

            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "Không thể sửa chính tài khoản của bạn.";
                return RedirectToAction("Index");
            }
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) 
            { 
                return NotFound();
            }
            var model = new UserEditViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                return NotFound();
            }

            // Cập nhật email nếu thay đổi
            if (user.Email != model.Email)
            {
                user.Email = model.Email;
                user.UserName = model.Email;
                var setEmailResult = await _userManager.UpdateAsync(user);

                if (!setEmailResult.Succeeded)
                {
                    foreach (var error in setEmailResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }
            }
            // Nếu có password mới, thì đổi password
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (!resetResult.Succeeded)
                {
                    foreach (var error in resetResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new IdentityUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    // Mặc định gán quyền User cho tài khoản mới
                    await _userManager.AddToRoleAsync(user, "User");
                    
                    TempData["Success"] = "Tạo người dùng thành công";
                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> AssignRole(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = _roleManager.Roles.Select(r => r.Name).ToList();

            ViewBag.UserId = userId;
            ViewBag.UserName = user.UserName;
            ViewBag.AllRoles = allRoles;
            ViewBag.UserRoles = userRoles.ToList();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, List<string> selectedRoles)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            
            // Xóa các quyền cũ
            var rolesToRemove = currentRoles.Except(selectedRoles ?? new List<string>()).ToList();
            if (rolesToRemove.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }

            // Thêm các quyền mới
            var rolesToAdd = (selectedRoles ?? new List<string>()).Except(currentRoles).ToList();
            if (rolesToAdd.Any())
            {
                await _userManager.AddToRolesAsync(user, rolesToAdd);
            }

            TempData["Success"] = "Cập nhật phân quyền thành công!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var currentUserId = _userManager.GetUserId(User); 

            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "Không thể tự khóa tài khoản của chính mình.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            bool isCurrentlyLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.Now;

            if (isCurrentlyLocked)
            {
                // Mở khóa
                user.LockoutEnd = null;
                await _userManager.UpdateAsync(user);
                TempData["SuccessMessage"] = "Đã mở khóa tài khoản thành công.";
            }
            else
            {
                // Khóa tài khoản
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.MaxValue;
                await _userManager.UpdateAsync(user);
                
                // Vô hiệu hóa ngay lập tức phiên đăng nhập hiện tại của người bị khóa
                await _userManager.UpdateSecurityStampAsync(user);
                
                TempData["SuccessMessage"] = "Đã khóa tài khoản thành công.";
            }

            return RedirectToAction("Index");
        }
    }
}
