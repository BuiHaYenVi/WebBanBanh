using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using DAWEB.Repository;
using DAWEB.Models;
using DAWEB.Services.Email;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DAWEB.Areas.Admin.Controllers
{
    [Authorize]
    [Area("Admin")]
    public class OrderController : Controller
    {
        private readonly DataContext _context;
        private readonly EmailService _emailService;

        public OrderController(DataContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: /Admin/Order
        public async Task<IActionResult> Index(string status, string search)
        {
            var query = _context.Orders.AsQueryable();

            // Tính toán thống kê nhanh
            ViewBag.TotalCount = await _context.Orders.CountAsync();
            ViewBag.PendingCount = await _context.Orders.CountAsync(o => o.Status == "Pending");
            ViewBag.CompletedCount = await _context.Orders.CountAsync(o => o.Status == "Completed");
            ViewBag.CancelledCount = await _context.Orders.CountAsync(o => o.Status == "Cancelled");

            // Bộ lọc trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.Status == status);
                ViewBag.CurrentStatus = status;
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(o => o.OrderCode.Contains(search) || o.ReceiverName.Contains(search) || o.UserName.Contains(search));
                ViewBag.CurrentSearch = search;
            }

            var orders = await query
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            return View(orders);
        }

        // GET: /Admin/Order/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
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

        // POST: /Admin/Order/Deliver/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deliver(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Pending")
            {
                TempData["ErrorMessage"] = "Đơn hàng này đã được xử lý từ trước.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            order.Status = "Delivering";
            
            // Nếu giao tại cửa hàng thì giao cho khách luôn (không qua quá trình shipper đi giao)
            if (order.DeliveryMethod == "Đến lấy trực tiếp")
            {
                order.Status = "Completed";
                order.PaymentStatus = "Paid";
            }

            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            // Lấy chi tiết đơn hàng và kích hoạt gửi email trong tiến trình chạy ngầm
            var orderDetails = await _context.OrderDetails.Where(d => d.OrderId == order.Id).ToListAsync();
            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendOrderNotificationEmailsAsync(order, orderDetails);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OrderController] Lỗi gửi email ngầm: {ex.Message}");
                }
            });

            TempData["SuccessMessage"] = order.Status == "Completed" ? "Khách đã nhận bánh thành công!" : "Đơn hàng đã được chuyển cho Shipper đi giao!";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // POST: /Admin/Order/ConfirmDelivered/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmDelivered(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status != "Delivering")
            {
                TempData["ErrorMessage"] = "Trạng thái không hợp lệ.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            order.Status = "Completed";
            order.PaymentStatus = "Paid"; // Đã giao xong tức là đã thu tiền COD
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xác nhận giao hàng thành công!";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }

        // POST: /Admin/Order/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status == "Cancelled")
            {
                TempData["ErrorMessage"] = "Đơn hàng này đã bị hủy từ trước.";
                return RedirectToAction(nameof(Details), new { id = order.Id });
            }

            // Hoàn lại số lượng sản phẩm tồn kho nếu trước đó chưa hủy
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

            order.Status = "Cancelled";
            _context.Orders.Update(order);

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
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
    }
}
