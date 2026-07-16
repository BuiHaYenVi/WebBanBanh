using DAWEB.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace DAWEB.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class StockController : Controller
    {
        private readonly DataContext _context;

        public StockController(DataContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Sắp xếp sản phẩm có số lượng tồn kho ít nhất lên đầu
            var products = _context.Products.OrderBy(p => p.ProductQuantity).ToList();
            return View(products);
        }

        [HttpPost]
        public async Task<IActionResult> AddStock(int id, int addQuantity)
        {
            if (addQuantity <= 0)
            {
                TempData["Error"] = "Số lượng nhập phải lớn hơn 0!";
                return RedirectToAction("Index");
            }

            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                TempData["Error"] = "Không tìm thấy sản phẩm!";
                return RedirectToAction("Index");
            }

            product.ProductQuantity += addQuantity;
            product.UpdateDate = System.DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã nhập thêm {addQuantity} cho sản phẩm {product.ProductName}!";
            return RedirectToAction("Index");
        }
    }
}
