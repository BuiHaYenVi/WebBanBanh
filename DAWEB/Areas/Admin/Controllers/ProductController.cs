using DAWEB.Models;
using DAWEB.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DAWEB.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin"), Area("Admin")]
    public class ProductController : Controller
    {
        private readonly DataContext _dbcontext;

        public ProductController(DataContext dbcontext) => _dbcontext = dbcontext;

        public async Task<IActionResult> Index() =>
            View(await _dbcontext.Products.Include(p => p.Category).OrderBy(p => p.Id).ToListAsync());

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadCategoriesToViewBag();
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductModels models, IFormFile? ImageUpload)
        {
            if (ImageUpload != null && ImageUpload.Length > 0)
            {
                ModelState.Remove("ImageUrl");
                models.ImageUrl = await SaveUploadedImage(ImageUpload);
            }

            ModelState.Remove("CreateDate");
            ModelState.Remove("UpdateDate");

            if (!ModelState.IsValid)
            {
                await LoadCategoriesToViewBag();
                return View(models);
            }

            models.CreateDate = DateTime.Now;
            models.UpdateDate = DateTime.Now;

            await _dbcontext.Products.AddAsync(models);
            await _dbcontext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Thêm bánh mới thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _dbcontext.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            await LoadCategoriesToViewBag();
            return View(product);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductModels models, int id, IFormFile? ImageUpload)
        {
            if (id != models.Id) return BadRequest();

            if (ImageUpload != null && ImageUpload.Length > 0)
            {
                ModelState.Remove("ImageUrl");
                models.ImageUrl = await SaveUploadedImage(ImageUpload);
            }
            else
            {
                var existingProduct = await _dbcontext.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                if (existingProduct != null && string.IsNullOrWhiteSpace(models.ImageUrl))
                {
                    models.ImageUrl = existingProduct.ImageUrl;
                }

                if (!string.IsNullOrWhiteSpace(models.ImageUrl)) ModelState.Remove("ImageUrl");
            }

            ModelState.Remove("CreateDate");
            ModelState.Remove("UpdateDate");

            if (!ModelState.IsValid)
            {
                await LoadCategoriesToViewBag();
                return View(models);
            }

            var product = await _dbcontext.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            // Cập nhật thông tin sản phẩm
            product.ProductName = models.ProductName;
            product.ProductDescription = models.ProductDescription;
            product.ProductPrice = models.ProductPrice;
            product.ProductColor = models.ProductColor;
            product.ProductSize = models.ProductSize;
            product.ProductMaterial = models.ProductMaterial;
            product.ProductGender = models.ProductGender;
            product.ProductQuantity = models.ProductQuantity;
            product.CategoryId = models.CategoryId;
            product.ImageUrl = models.ImageUrl;
            product.UpdateDate = DateTime.Now;

            await _dbcontext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật thông tin bánh thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken] // Thêm bảo mật chống tấn công giả mạo CSRF cho hàm xóa
        public async Task<IActionResult> Delete(int? id)
        {
            var product = await _dbcontext.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound();

            // Kiểm tra ràng buộc dữ liệu đơn hàng (Xóa cứng an toàn)
            if (await _dbcontext.OrderDetails.AnyAsync(od => od.ProductId == id))
            {
                TempData["ErrorMessage"] = "Không thể xóa cứng bánh này vì đã có khách hàng đặt mua (Cần giữ lại cho lịch sử đơn hàng)!";
                return RedirectToAction(nameof(Index));
            }

            _dbcontext.Products.Remove(product);
            await _dbcontext.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa bánh thành công!";
            return RedirectToAction(nameof(Index));
        }

        #region Helper Methods (Hàm bổ trợ rút gọn code trùng lặp)

        private async Task LoadCategoriesToViewBag() =>
            ViewBag.Categories = await _dbcontext.Categories.OrderBy(c => c.Id).ToListAsync();

        private async Task<string> SaveUploadedImage(IFormFile file)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/images/{uniqueFileName}";
        }

        #endregion
    }
}