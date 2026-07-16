using DAWEB.Models;
using DAWEB.Repository;
using DAWEB.Services.Email;
using DAWEB.Services.PayOS;
using Microsoft.EntityFrameworkCore;

namespace DAWEB.Services
{
    public class OrderService
    {
        private readonly DataContext _context;
        private readonly EmailService _emailService;
        private readonly PayOSService _payOSService;

        public OrderService(DataContext context, EmailService emailService, PayOSService payOSService)
        {
            _context = context;
            _emailService = emailService;
            _payOSService = payOSService;
        }

        public async Task<(bool Success, string OrderCode, string Message)> CreateOrderAsync(OrderModel order, List<CartItem> cartItems, string userName)
        {
            if (cartItems == null || !cartItems.Any())
            {
                return (false, string.Empty, "Giỏ hàng trống.");
            }

            string orderCode = "SB-" + new Random().Next(100000, 999999).ToString();
            order.OrderCode = orderCode;
            order.UserName = userName;
            order.CreatedDate = DateTime.Now;
            
            decimal cartTotal = cartItems.Sum(x => x.Price * x.Quantity);
            decimal discountAmount = 0;

            if (!string.IsNullOrEmpty(order.VoucherCode))
            {
                var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.Code == order.VoucherCode);
                if (voucher != null && voucher.IsActive && voucher.StartDate <= DateTime.Now && voucher.EndDate >= DateTime.Now && voucher.UsedCount < voucher.UsageLimit && cartTotal >= voucher.MinimumOrderAmount)
                {
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

                    if (discountAmount > cartTotal) discountAmount = cartTotal;

                    order.DiscountAmount = discountAmount;
                    voucher.UsedCount += 1;
                    _context.Vouchers.Update(voucher);
                }
                else
                {
                    order.VoucherCode = null; // Bỏ qua voucher nếu không hợp lệ
                }
            }

            if (order.DeliveryMethod == "Giao tận nơi")
            {
                order.ShippingFee = 30000;
            }
            else
            {
                order.ShippingFee = 0;
                // Nếu khách chọn đến lấy, lưu tên tiệm thay vì địa chỉ trống
                order.ReceiverAddress = "Nhận tại cửa hàng: ";
            }

            order.TotalAmount = cartTotal - discountAmount + order.ShippingFee;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var item in cartItems)
                {
                    var orderDetail = new OrderDetailModel
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Price = item.Price,
                        Quantity = item.Quantity
                    };
                    _context.OrderDetails.Add(orderDetail);

                    // Cập nhật tồn kho sản phẩm
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        product.ProductQuantity = Math.Max(0, product.ProductQuantity - item.Quantity);
                        _context.Products.Update(product);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return (true, orderCode, "Đặt hàng thành công!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, string.Empty, $"Lỗi khi tạo đơn hàng: {ex.Message}");
            }
        }

        public async Task<bool> CheckAndUpdatePayOSPaymentStatusAsync(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null || order.PaymentMethod != "PayOS") return false;

            if (order.Status == "Completed" && order.PaymentStatus == "Paid") return true;

            try
            {
                long payOsOrderId = long.Parse(new string(order.OrderCode.Where(char.IsDigit).ToArray()));
                string? payosStatus = await _payOSService.GetPaymentStatusAsync(payOsOrderId);
                if (string.Equals(payosStatus, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    order.PaymentStatus = "Paid";
                    order.Status = "Completed";
                    _context.Orders.Update(order);
                    await _context.SaveChangesAsync();

                    // Lấy chi tiết đơn hàng và kích hoạt gửi email
                    var orderDetailsList = await _context.OrderDetails.Where(d => d.OrderId == order.Id).ToListAsync();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendOrderNotificationEmailsAsync(order, orderDetailsList);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[OrderService] Lỗi gửi email ngầm PayOS: {ex.Message}");
                        }
                    });

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OrderService] Lỗi kiểm tra trạng thái PayOS: {ex.Message}");
            }

            return false;
        }
    }
}
