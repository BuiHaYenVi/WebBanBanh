using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DAWEB.Repository;
using DAWEB.Services.PayOS;
using Microsoft.EntityFrameworkCore;

namespace DAWEB.Controllers
{
    /// <summary>
    /// Controller quản lý luồng gửi yêu cầu thanh toán sang cổng PayOS
    /// </summary>
    public class PaymentController : Controller
    {
        private readonly DataContext _context;
        private readonly PayOSService _payOSService;

        public PaymentController(DataContext context, PayOSService payOSService)
        {
            _context = context;
            _payOSService = payOSService;
        }

        // Admin cũng được phép truy cập trang thanh toán

        /// <summary>
        /// Tạo link thanh toán PayOS và chuyển hướng người dùng sang giao diện thanh toán
        /// </summary>
        /// <param name="orderId">Id đơn hàng vừa tạo trong database</param>
        [HttpGet]
        public async Task<IActionResult> CreatePayOSPayment(int orderId)
        {
            // 1. Tìm đơn hàng trong cơ sở dữ liệu
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn hàng cần thanh toán.";
                return RedirectToAction("Index", "Cart");
            }

            // 2. Thiết lập URL nhận kết quả trả về khi khách bấm quay lại hoặc hủy bỏ
            // Sử dụng Scheme (http/https) và Host hiện tại của website để sinh URL động tự động
            string returnUrl = Url.Action("OrderSuccess", "Cart", new { orderCode = order.OrderCode }, Request.Scheme) ?? "";
            string cancelUrl = Url.Action("Index", "Cart", null, Request.Scheme) ?? "";

            // Nội dung chuyển khoản ngắn gọn, không dấu (PayOS giới hạn tối đa 25 ký tự)
            string description = $"Thanh toan DH {order.Id}";

            long payOsOrderId = long.Parse(new string(order.OrderCode.Where(char.IsDigit).ToArray()));

            // 3. Gửi yêu cầu tạo link thanh toán sang PayOS
            // Chúng ta truyền: payOsOrderId (được trích xuất từ OrderCode) làm mã đơn hàng độc nhất
            var paymentResult = await _payOSService.CreatePaymentLinkAsync(
                orderId: payOsOrderId,
                amount: order.TotalAmount,
                description: description,
                cancelUrl: cancelUrl,
                returnUrl: returnUrl
            );

            if (paymentResult == null || string.IsNullOrEmpty(paymentResult.CheckoutUrl))
            {
                TempData["ErrorMessage"] = "Không thể kết nối tới cổng thanh toán PayOS. Vui lòng thử lại sau.";
                return RedirectToAction("OrderSuccess", "Cart", new { orderCode = order.OrderCode });
            }

            // 4. Cập nhật phương thức thanh toán thành PayOS (nếu cần) và lưu
            order.PaymentMethod = "PayOS";
            _context.Orders.Update(order);
            await _context.SaveChangesAsync();

            // 5. Chuyển hướng người dùng sang trang thanh toán của PayOS
            return Redirect(paymentResult.CheckoutUrl);
        }
    }
}
