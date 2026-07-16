using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DAWEB.Repository;
using DAWEB.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DAWEB.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly DataContext _context;

        public OrderController(DataContext context)
        {
            _context = context;
        }

        // Admin cũng được phép xem đơn hàng cá nhân

        // GET: /Order
        public async Task<IActionResult> Index()
        {
            var userName = User.Identity?.Name;
            var orders = await _context.Orders
                .Where(o => o.UserName == userName)
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Order/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var userName = User.Identity?.Name;
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserName == userName);

            if (order == null)
            {
                return NotFound();
            }

            var orderDetails = await _context.OrderDetails
                .Include(d => d.Product)
                .Where(d => d.OrderId == order.Id)
                .ToListAsync();

            ViewBag.OrderDetails = orderDetails;
            return View(order);
        }

        // POST: /Order/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userName = User.Identity?.Name;
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserName == userName);

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Chỉ có thể hủy đơn hàng ở trạng thái Chờ duyệt.";
                return RedirectToAction(nameof(Index));
            }

            order.Status = "Cancelled";
            _context.Orders.Update(order);

            // Hoàn lại số lượng sản phẩm tồn kho
            var orderDetails = await _context.OrderDetails
                .Where(d => d.OrderId == order.Id)
                .ToListAsync();

            foreach (var detail in orderDetails)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == detail.ProductId);
                if (product != null)
                {
                    product.ProductQuantity += detail.Quantity;
                    _context.Products.Update(product);
                }
            }

            // Hoàn lại lượt sử dụng Voucher
            if (!string.IsNullOrEmpty(order.VoucherCode))
            {
                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == order.VoucherCode);
                if (voucher != null && voucher.UsedCount > 0)
                {
                    voucher.UsedCount -= 1;
                    _context.Vouchers.Update(voucher);
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Hủy đơn hàng thành công!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Order/ConfirmReceived/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int id)
        {
            var userName = User.Identity?.Name;
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == id && o.UserName == userName);

            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Delivering")
            {
                TempData["ErrorMessage"] = "Không thể thực hiện thao tác này.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            order.Status = "Completed";
            order.PaymentStatus = "Paid";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cảm ơn bạn đã xác nhận! Chúc bạn ăn bánh ngon miệng.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
    }
}
