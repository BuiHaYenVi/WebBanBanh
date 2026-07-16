using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DAWEB.Models;
using DAWEB.Repository; // Thêm dòng này để nhận diện file DataContext

namespace DAWEB.Controllers
{
    public class ProductController : Controller
    {
        // Thay DbContext thành DataContext
        private readonly DataContext _context;

        public ProductController(DataContext context)
        {
            _context = context;
        }

        // Admin cũng được phép xem trang sản phẩm

        // 1. Trang danh sách sản phẩm
        public async Task<IActionResult> Index(string category, string gender, string search)
        {
            var filter = !string.IsNullOrEmpty(category) ? category : gender;
            var query = _context.Products.Where(p => !p.IsDeleted).AsQueryable();

            CategoryModel? selectedCategory = null;

            if (!string.IsNullOrEmpty(filter))
            {
                if (int.TryParse(filter, out int catId))
                {
                    selectedCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Id == catId);
                }
                else
                {
                    string slugToSearch = filter.ToLower() switch
                    {
                        "nam" => "banhkem",
                        "nu" => "banhngot",
                        "unisex" => "banhmi",
                        "banhkem" => "banhkem",
                        "banhngot" => "banhngot",
                        "banhmi" => "banhmi",
                        _ => filter.ToLower()
                    };

                    selectedCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Slug == slugToSearch || c.Name.ToLower() == slugToSearch);
                }

                if (selectedCategory != null)
                {
                    query = query.Where(p => p.CategoryId == selectedCategory.Id);
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.ProductName.Contains(search));
                ViewBag.CurrentSearch = search;
            }

            var products = await query.ToListAsync();
            var categories = await _context.Categories.OrderBy(c => c.Id).ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.SelectedCategory = selectedCategory;
            ViewBag.CurrentCategoryFilter = filter;

            return View(products);
        }

        // 2. Trang chi tiết sản phẩm
        public async Task<IActionResult> Detail(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }
    }
}