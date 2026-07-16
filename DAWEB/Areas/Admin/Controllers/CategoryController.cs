using DAWEB.Models;
using DAWEB.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace DAWEB.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin"), Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly DataContext _dbcontext;

        public CategoryController(DataContext dbcontext) => _dbcontext = dbcontext;

        public async Task<IActionResult> Index() =>
            View(await _dbcontext.Categories.OrderBy(c => c.Id).ToListAsync());

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryModel category)
        {
            if (string.IsNullOrWhiteSpace(category.Slug) && !string.IsNullOrWhiteSpace(category.Name))
                category.Slug = GenerateSlug(category.Name);

            if (!string.IsNullOrWhiteSpace(category.Slug)) ModelState.Remove("Slug");

            if (!ModelState.IsValid) return View(category);

            await _dbcontext.Categories.AddAsync(category);
            await _dbcontext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Thêm danh mục mới thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var category = await _dbcontext.Categories.FindAsync(id);
            return category == null ? NotFound() : View(category);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CategoryModel category)
        {
            if (id != category.Id) return BadRequest();

            if (string.IsNullOrWhiteSpace(category.Slug) && !string.IsNullOrWhiteSpace(category.Name))
                category.Slug = GenerateSlug(category.Name);

            if (!string.IsNullOrWhiteSpace(category.Slug)) ModelState.Remove("Slug");

            if (!ModelState.IsValid) return View(category);

            _dbcontext.Update(category);
            await _dbcontext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _dbcontext.Categories.FindAsync(id);
            if (category == null) return NotFound();

            // Kiểm tra sản phẩm chưa bị xóa mềm (IsDeleted == false)
            if (await _dbcontext.Products.AnyAsync(p => p.CategoryId == id && !p.IsDeleted))
            {
                TempData["ErrorMessage"] = "Không thể xóa danh mục này vì đang có sản phẩm thuộc danh mục!";
                return RedirectToAction(nameof(Index));
            }

            _dbcontext.Categories.Remove(category);
            await _dbcontext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Xóa danh mục thành công!";
            return RedirectToAction(nameof(Index));
        }

        private string GenerateSlug(string text)
        {
            string str = text.ToLowerInvariant();
            string[] signed = { "á", "à", "ả", "ã", "ạ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "đ", "é", "è", "ẻ", "ẽ", "ẹ", "ê", "ế", "ề", "ể", "ễ", "ệ", "í", "ì", "ỉ", "ĩ", "ị", "ó", "ò", "ỏ", "õ", "ọ", "ô", "ố", "ồ", "ổ", "ỗ", "ộ", "ơ", "ớ", "ờ", "ở", "ỡ", "ợ", "ú", "ù", "ủ", "ũ", "ụ", "ư", "ứ", "ừ", "ử", "ữ", "ự", "ý", "ỳ", "ỷ", "ỹ", "ỵ" };
            string[] unsigned = { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "d", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "e", "i", "i", "i", "i", "i", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "o", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "u", "y", "y", "y", "y", "y" };

            for (int i = 0; i < signed.Length; i++)
                str = str.Replace(signed[i], unsigned[i]);

            str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
            return Regex.Replace(str, @"\s+", " ").Trim().Replace(" ", "-");
        }
    }
}