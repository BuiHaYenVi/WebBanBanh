using DAWEB.Models;
using DAWEB.Repository;
using Microsoft.AspNetCore.Mvc;
using DAWEB.Services;
using DAWEB.Services.PayOS;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace DAWEB.Controllers
{
    public class CartController : Controller
    {
        private readonly DataContext _context;
        private readonly PayOSService _payOSService;
        private readonly CartService _cartService;
        private readonly OrderService _orderService;

        public CartController(DataContext context, PayOSService payOSService, CartService cartService, OrderService orderService)
        {
            _context = context;
            _payOSService = payOSService;
            _cartService = cartService;
            _orderService = orderService;
        }

        // Admin cũng được phép sử dụng giỏ hàng

        // Hiển thị giỏ hàng
        public IActionResult Index()
        {
            var cartItems = _cartService.GetCartItems();

            // Lấy thông tin tồn kho cho các sản phẩm trong giỏ hàng
            var productIds = cartItems.Select(x => x.ProductId).ToList();
            var productStocks = _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.ProductQuantity);

            ViewBag.ProductStocks = productStocks;

            return View(cartItems);
        }

        // Thêm vào giỏ hàng
        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity, bool buyNow = false)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.";
                return RedirectToAction("Login", "Account");
            }

            var result = _cartService.AddToCart(productId, quantity);
            
            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction("Index", "Product");
            }

            if (buyNow)
            {
                return RedirectToAction("Checkout");
            }

            return RedirectToAction("Index");
        }

        // Cập nhật số lượng sản phẩm bằng AJAX
        [HttpPost]
        public IActionResult UpdateQuantity(int productId, int quantity)
        {
            var result = _cartService.UpdateQuantity(productId, quantity);

            if (!result.Success)
            {
                if (result.MaxQuantity > 0)
                {
                    return Json(new { success = false, message = result.Message, maxQuantity = result.MaxQuantity });
                }
                return Json(new { success = false, message = result.Message });
            }

            var cartItems = _cartService.GetCartItems();
            var item = cartItems.First(x => x.ProductId == productId);
            
            return Json(new { 
                success = true, 
                itemTotal = (item.Price * item.Quantity).ToString("N0") + " đ",
                cartTotal = _cartService.GetTotalAmount().ToString("N0") + " đ",
                cartCount = _cartService.GetCartCount()
            });
        }

        // Thêm vào giỏ hàng bằng AJAX
        [HttpPost]
        public IActionResult AddToCartAjax(int productId, int quantity)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.", requireLogin = true });
            }

            var result = _cartService.AddToCart(productId, quantity);
            
            if (!result.Success)
            {
                return Json(new { success = false, message = result.Message });
            }

            return Json(new { success = true, message = result.Message, cartCount = _cartService.GetCartCount() });
        }

        // Xóa sản phẩm khỏi giỏ hàng
        public IActionResult RemoveFromCart(int productId)
        {
            _cartService.RemoveFromCart(productId);
            return RedirectToAction("Index");
        }

        // Hiển thị trang thanh toán
        public IActionResult Checkout()
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập trước khi thanh toán.";
                return RedirectToAction("Login", "Account");
            }

            var cartItems = _cartService.GetCartItems();

            if (cartItems.Count == 0)
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn đang trống.";
                return RedirectToAction("Index");
            }

            ViewBag.CartItems = cartItems;
            ViewBag.TotalAmount = _cartService.GetTotalAmount();

            var order = new OrderModel
            {
                ReceiverName = User.Identity.Name ?? "",
                UserName = User.Identity.Name ?? ""
            };

            return View(order);
        }

        // Xử lý đặt hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(OrderModel order)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            Console.WriteLine($"[DEBUG CHECKOUT] Incoming VoucherCode: '{order.VoucherCode}'");

            var cartItems = _cartService.GetCartItems();

            if (cartItems.Count == 0)
            {
                TempData["ErrorMessage"] = "Giỏ hàng trống, không thể thanh toán.";
                return RedirectToAction("Index");
            }

            // Kiểm tra xem có sản phẩm nào đã bị xóa mềm hoặc ngưng kinh doanh không
            var productIds = cartItems.Select(x => x.ProductId).ToList();
            var deletedProductIds = await _context.Products
                .Where(p => productIds.Contains(p.Id) && p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync();

            if (deletedProductIds.Any())
            {
                TempData["ErrorMessage"] = "Giỏ hàng của bạn chứa sản phẩm đã ngừng kinh doanh. Vui lòng kiểm tra lại giỏ hàng.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.CartItems = cartItems;
                ViewBag.TotalAmount = _cartService.GetTotalAmount();
                return View(order);
            }

            var result = await _orderService.CreateOrderAsync(order, cartItems, User.Identity.Name ?? "");
            
            if (result.Success)
            {
                _cartService.ClearCart();
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction("OrderSuccess", new { orderCode = result.OrderCode });
            }
            else
            {
                TempData["ErrorMessage"] = result.Message;
                return RedirectToAction("Index");
            }
        }

        // Trang đặt hàng thành công
        public async Task<IActionResult> OrderSuccess(string orderCode)
        {
            if (User.Identity == null || !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode && o.UserName == User.Identity.Name);
            if (order == null)
            {
                return RedirectToAction("Index", "Home");
            }

            if (order.PaymentMethod == "PayOS")
            {
                try
                {
                    long payOsOrderId = long.Parse(new string(order.OrderCode.Where(char.IsDigit).ToArray()));

                    string returnUrl = Url.Action("OrderSuccess", "Cart", new { orderCode = order.OrderCode }, Request.Scheme) ?? "";
                    string cancelUrl = Url.Action("Index", "Cart", null, Request.Scheme) ?? "";
                    string description = $"Thanh toan DH {order.Id}";

                    var paymentResult = await _payOSService.CreatePaymentLinkAsync(
                        orderId: payOsOrderId,
                        amount: order.TotalAmount,
                        description: description,
                        cancelUrl: cancelUrl,
                        returnUrl: returnUrl
                    );

                    if (paymentResult == null)
                    {
                        paymentResult = await _payOSService.GetPaymentLinkDetailsAsync(payOsOrderId);
                    }

                    if (paymentResult != null)
                    {
                        ViewBag.PayOSDetails = paymentResult;
                    }
                    
                    await _orderService.CheckAndUpdatePayOSPaymentStatusAsync(order.Id);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[OrderSuccess] Lỗi xử lý PayOS: {ex.Message}");
                }
            }

            var orderDetails = await _context.OrderDetails.Where(d => d.OrderId == order.Id).ToListAsync();

            ViewBag.OrderDetails = orderDetails;
            return View(order);
        }

        // API lấy dữ liệu giỏ hàng dưới dạng JSON để phục vụ hover cart dropdown
        [HttpGet]
        public IActionResult GetCartData()
        {
            var cartItems = _cartService.GetCartItems();

            var productIds = cartItems.Select(x => x.ProductId).ToList();
            var products = _context.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionary(p => p.Id, p => p.ImageUrl);

            var items = cartItems.Select(item => new {
                productId = item.ProductId,
                productName = item.ProductName,
                price = item.Price,
                priceFormatted = item.Price.ToString("N0") + " đ",
                quantity = item.Quantity,
                imageUrl = products.ContainsKey(item.ProductId) ? products[item.ProductId] : "/images/no-image.png",
                itemTotal = (item.Price * item.Quantity).ToString("N0") + " đ"
            }).ToList();

            return Json(new {
                success = true,
                items = items,
                cartTotal = _cartService.GetTotalAmount(),
                cartTotalFormatted = _cartService.GetTotalAmount().ToString("N0") + " đ",
                cartCount = _cartService.GetCartCount()
            });
        }

        // API kiểm tra trạng thái thanh toán đơn hàng (auto-polling)
        [HttpGet]
        public async Task<IActionResult> CheckOrderStatus(string orderCode)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderCode == orderCode);
            if (order == null)
            {
                return Json(new { success = false, message = "Không tìm thấy đơn hàng." });
            }

            await _orderService.CheckAndUpdatePayOSPaymentStatusAsync(order.Id);

            return Json(new { success = true, status = order.Status, paymentStatus = order.PaymentStatus });
        }

        // API xóa nhanh sản phẩm khỏi giỏ hàng qua AJAX từ hover cart
        [HttpPost]
        public IActionResult RemoveFromCartAjax(int productId)
        {
            var cartItems = _cartService.GetCartItems();
            if (cartItems.Any())
            {
                _cartService.RemoveFromCart(productId);
                
                return Json(new {
                    success = true,
                    message = "Đã xóa sản phẩm thành công.",
                    cartCount = _cartService.GetCartCount(),
                    cartTotalFormatted = _cartService.GetTotalAmount().ToString("N0") + " đ"
                });
            }

            return Json(new { success = false, message = "Giỏ hàng rỗng." });
        }

        // API kiểm tra và áp dụng mã giảm giá
        [HttpPost]
        public async Task<IActionResult> ApplyVoucher(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Json(new { success = false, message = "Vui lòng nhập mã giảm giá." });

            code = code.ToUpper();
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == code);

            if (voucher == null)
                return Json(new { success = false, message = "Mã giảm giá không tồn tại." });

            if (!voucher.IsActive)
                return Json(new { success = false, message = "Mã giảm giá đã bị vô hiệu hóa." });

            var now = DateTime.Now;
            if (now < voucher.StartDate)
                return Json(new { success = false, message = "Mã giảm giá chưa đến ngày sử dụng." });

            if (now > voucher.EndDate)
                return Json(new { success = false, message = "Mã giảm giá đã hết hạn." });

            if (voucher.UsedCount >= voucher.UsageLimit)
                return Json(new { success = false, message = "Mã giảm giá đã hết lượt sử dụng." });

            var cartTotal = _cartService.GetTotalAmount();
            if (cartTotal < voucher.MinimumOrderAmount)
                return Json(new { success = false, message = $"Đơn hàng tối thiểu phải từ {voucher.MinimumOrderAmount.ToString("N0")}đ để áp dụng mã này." });

            decimal discountAmount = 0;
            if (voucher.DiscountType == "Fixed")
            {
                discountAmount = voucher.DiscountValue;
            }
            else if (voucher.DiscountType == "Percent")
            {
                discountAmount = cartTotal * (voucher.DiscountValue / 100);
                if (voucher.MaxDiscountAmount.HasValue && discountAmount > voucher.MaxDiscountAmount.Value)
                {
                    discountAmount = voucher.MaxDiscountAmount.Value;
                }
            }

            if (discountAmount > cartTotal)
            {
                discountAmount = cartTotal;
            }

            var newTotal = cartTotal - discountAmount;

            return Json(new { 
                success = true, 
                message = "Áp dụng mã thành công!", 
                discountAmount = discountAmount,
                discountFormatted = discountAmount.ToString("N0") + " đ",
                newTotal = newTotal,
                newTotalFormatted = newTotal.ToString("N0") + " đ",
                code = voucher.Code
            });
        }
    }
}