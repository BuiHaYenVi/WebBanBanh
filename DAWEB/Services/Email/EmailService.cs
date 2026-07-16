using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DAWEB.Models;

namespace DAWEB.Services.Email
{
    /// <summary>
    /// Service hỗ trợ gửi Email tự động qua giao thức SMTP (ví dụ Gmail SMTP)
    /// </summary>
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _port;
        private readonly string _senderEmail;
        private readonly string _senderName;
        private readonly string _password;
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            // Đọc thông số từ file cấu hình appsettings.json
            _smtpServer = configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
            _port = int.Parse(configuration["EmailSettings:Port"] ?? "587");
            _senderEmail = configuration["EmailSettings:SenderEmail"] ?? "";
            _senderName = configuration["EmailSettings:SenderName"] ?? "Tiệm Bánh Của Bạn";
            _password = configuration["EmailSettings:Password"] ?? "";
        }

        /// <summary>
        /// Gửi một Email dạng HTML bất đồng bộ
        /// </summary>
        /// <param name="toEmail">Địa chỉ email người nhận</param>
        /// <param name="subject">Tiêu đề email</param>
        /// <param name="bodyHtml">Nội dung email dạng HTML</param>
        public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml)
        {
            // Kiểm tra cấu hình email trước khi gửi
            if (string.IsNullOrEmpty(_senderEmail) || string.IsNullOrEmpty(_password))
            {
                Console.WriteLine("Cảnh báo: Chưa cấu hình thông tin Email (SenderEmail/Password) trong appsettings.json. Gửi email bị bỏ qua.");
                return;
            }

            try
            {
                // Cấu hình SMTP Client
                using (var client = new SmtpClient(_smtpServer, _port))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(_senderEmail, _password);
                    client.EnableSsl = true; // Yêu cầu bảo mật SSL/TLS (cổng 587)

                    // Tạo nội dung mail
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_senderEmail, _senderName),
                        Subject = subject,
                        Body = bodyHtml,
                        IsBodyHtml = true, // Bật chế độ vẽ HTML (hỗ trợ css, bảng biểu, hình ảnh)
                        BodyEncoding = Encoding.UTF8,
                        SubjectEncoding = Encoding.UTF8
                    };
                    
                    mailMessage.To.Add(toEmail);

                    // Gửi email bất đồng bộ
                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine($"[EmailService] Đã gửi email thành công tới: {toEmail}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailService] Thất bại khi gửi email tới {toEmail}. Lỗi chi tiết: {ex.Message}");
            }
        }

        /// <summary>
        /// Tạo và gửi email xác nhận cho chủ cửa hàng/bếp và khách hàng khi đơn hàng hoàn thành thanh toán
        /// </summary>
        public async Task SendOrderNotificationEmailsAsync(OrderModel order, List<OrderDetailModel> details)
        {
            // 1. Lấy thông tin email từ cấu hình
            string kitchenEmail = _configuration["EmailSettings:KitchenEmail"] ?? "";
            string customerEmail = order.UserName; // UserName liên kết với Email đặt bánh
            
            if (string.IsNullOrEmpty(customerEmail) || !customerEmail.Contains("@"))
            {
                customerEmail = "customer-demo@gmail.com"; // Fallback nếu tài khoản không phải email
            }

            // 2. Tạo bảng danh sách bánh dạng HTML
            var itemsHtmlBuilder = new StringBuilder();
            itemsHtmlBuilder.Append("<table style='width:100%; border-collapse:collapse; margin-top:10px;'>");
            itemsHtmlBuilder.Append("<tr style='background-color:#f8f9fa;'>");
            itemsHtmlBuilder.Append("<th style='border:1px solid #ddd; padding:8px; text-align:left;'>Tên bánh</th>");
            itemsHtmlBuilder.Append("<th style='border:1px solid #ddd; padding:8px; text-align:center;'>Số lượng</th>");
            itemsHtmlBuilder.Append("<th style='border:1px solid #ddd; padding:8px; text-align:right;'>Đơn giá</th>");
            itemsHtmlBuilder.Append("<th style='border:1px solid #ddd; padding:8px; text-align:right;'>Thành tiền</th>");
            itemsHtmlBuilder.Append("</tr>");

            foreach (var item in details)
            {
                decimal subtotal = item.Price * item.Quantity;
                itemsHtmlBuilder.Append("<tr>");
                itemsHtmlBuilder.Append($"<td style='border:1px solid #ddd; padding:8px;'>{item.ProductName}</td>");
                itemsHtmlBuilder.Append($"<td style='border:1px solid #ddd; padding:8px; text-align:center;'>{item.Quantity}</td>");
                itemsHtmlBuilder.Append($"<td style='border:1px solid #ddd; padding:8px; text-align:right;'>{item.Price:N0}đ</td>");
                itemsHtmlBuilder.Append($"<td style='border:1px solid #ddd; padding:8px; text-align:right;'>{subtotal:N0}đ</td>");
                itemsHtmlBuilder.Append("</tr>");
            }
            
            if (order.DiscountAmount > 0)
            {
                itemsHtmlBuilder.Append("<tr>");
                itemsHtmlBuilder.Append($"<td colspan='3' style='border:1px solid #ddd; padding:8px; text-align:right;'><strong>Giảm giá Voucher ({(string.IsNullOrEmpty(order.VoucherCode) ? "" : order.VoucherCode)})</strong></td>");
                itemsHtmlBuilder.Append($"<td style='border:1px solid #ddd; padding:8px; text-align:right; color:#e53e3e;'>-{order.DiscountAmount:N0}đ</td>");
                itemsHtmlBuilder.Append("</tr>");
            }
            
            itemsHtmlBuilder.Append("<tr>");
            itemsHtmlBuilder.Append($"<td colspan='3' style='border:1px solid #ddd; padding:8px; text-align:right;'><strong>Phí giao hàng ({order.DeliveryMethod})</strong></td>");
            itemsHtmlBuilder.Append($"<td style='border:1px solid #ddd; padding:8px; text-align:right;'>{(order.ShippingFee == 0 ? "Miễn phí" : order.ShippingFee.ToString("N0") + "đ")}</td>");
            itemsHtmlBuilder.Append("</tr>");

            itemsHtmlBuilder.Append("</table>");
            string itemsHtml = itemsHtmlBuilder.ToString();

            // 3. Gửi Email thông báo cho nhà bếp/chủ cửa hàng
            if (!string.IsNullOrEmpty(kitchenEmail))
            {
                string kitchenSubject = $"🔔 Yêu Cầu Làm Bánh Mới - Đơn hàng #{order.OrderCode}";
                string kitchenBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                        <h2 style='color: #2b6cb0; border-bottom: 2px solid #2b6cb0; padding-bottom: 10px;'>YÊU CẦU LÀM BÁNH MỚI</h2>
                        <p>Đơn hàng <strong>#{order.OrderCode}</strong> đã thanh toán thành công và được xác nhận.</p>
                        <h3>Chi tiết giao hàng:</h3>
                        <ul>
                            <li><strong>Khách hàng:</strong> {order.ReceiverName}</li>
                            <li><strong>Số điện thoại:</strong> {order.ReceiverPhone}</li>
                            <li><strong>Địa chỉ giao:</strong> {order.ReceiverAddress}</li>
                            <li><strong>Thời gian đặt:</strong> {order.CreatedDate:dd/MM/yyyy HH:mm:ss}</li>
                        </ul>
                        <h3>Danh sách bánh cần làm:</h3>
                        {itemsHtml}
                        <p style='text-align: right; font-size: 1.1em;'><strong>Tổng tiền đơn hàng:</strong> <span style='color: #e53e3e;'>{order.TotalAmount:N0}đ</span></p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                        <p style='font-size: 0.9em; color: #718096; text-align: center;'>Vui lòng bắt đầu sản xuất bánh và chuẩn bị giao hàng đúng hẹn.</p>
                    </div>";

                await SendEmailAsync(kitchenEmail, kitchenSubject, kitchenBody);
            }

            // 4. Gửi Email xác nhận cho khách hàng
            if (!string.IsNullOrEmpty(customerEmail))
            {
                string customerSubject = $"🎉 Xác Nhận Thanh Toán - Đơn hàng #{order.OrderCode}";
                string customerBody = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                        <div style='text-align: center;'>
                            <h1 style='color: #48bb78; margin-bottom: 5px;'>Xác Nhận Thanh Toán Thành Công!</h1>
                            <p style='font-size: 1.1em; color: #4a5568;'>Chào {order.ReceiverName}, tiệm bánh đã nhận được thanh toán của bạn.</p>
                        </div>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                        <p style='font-size: 1.1em; color: #2d3748;'>🎂 <strong>Bánh của bạn đang được chuẩn bị làm!</strong></p>
                        <p>Đội ngũ đầu bếp của chúng tôi đã nhận được thông tin và bắt đầu chế biến những mẻ bánh tươi ngon nhất cho bạn. Bánh sẽ được giao đến bạn theo thông tin sau:</p>
                        <div style='background-color: #f7fafc; padding: 15px; border-radius: 5px; margin-bottom: 20px;'>
                            <p style='margin: 5px 0;'><strong>Mã đơn hàng:</strong> #{order.OrderCode}</p>
                            <p style='margin: 5px 0;'><strong>Người nhận:</strong> {order.ReceiverName}</p>
                            <p style='margin: 5px 0;'><strong>Số điện thoại:</strong> {order.ReceiverPhone}</p>
                            <p style='margin: 5px 0;'><strong>Địa chỉ nhận bánh:</strong> {order.ReceiverAddress}</p>
                        </div>
                        <h3>Tóm tắt đơn hàng của bạn:</h3>
                        {itemsHtml}
                        <p style='text-align: right; font-size: 1.1em;'><strong>Tổng tiền đã thanh toán:</strong> <span style='color: #48bb78;'>{order.TotalAmount:N0}đ</span></p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                        <p style='text-align: center; font-size: 0.9em; color: #718096;'>Cảm ơn bạn đã lựa chọn tiệm bánh của chúng tôi! Chúc bạn một ngày ngọt ngào!</p>
                    </div>";

                await SendEmailAsync(customerEmail, customerSubject, customerBody);
            }
        }
    }
}
