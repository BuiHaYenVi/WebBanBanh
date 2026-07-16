using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using DAWEB.Models;
using DAWEB.Repository;
using DAWEB.Services.Email;
using DAWEB.Services.PayOS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;

namespace DAWEB.Controllers
{
    /// <summary>
    /// API Controller lắng nghe kết quả thanh toán tự động trả về từ PayOS và SePay
    /// </summary>
    [ApiController]
    [Route("api/webhooks")]
    public class PaymentWebhookController : ControllerBase
    {
        private readonly DataContext _context;
        private readonly PayOSService _payOSService;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public PaymentWebhookController(DataContext context, PayOSService payOSService, EmailService emailService, IConfiguration configuration, IWebHostEnvironment env)
        {
            _context = context;
            _payOSService = payOSService;
            _emailService = emailService;
            _configuration = configuration;
            _env = env;
        }

        /// <summary>
        /// Webhook nhận kết quả từ PayOS
        /// Địa chỉ endpoint: POST /api/webhooks/payos
        /// </summary>
        [HttpPost("payos")]
        public async Task<IActionResult> ReceivePayOSWebhook()
        {
            // 1. Đọc dữ liệu JSON thô gửi từ PayOS
            string requestBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            if (string.IsNullOrEmpty(requestBody))
            {
                return BadRequest(new { success = false, message = "Payload rỗng." });
            }

            // 2. Xác thực chữ ký bảo mật từ PayOS để tránh tin tặc gửi yêu cầu giả mạo
            bool isSignatureValid = _payOSService.VerifyWebhookData(requestBody);
            if (!isSignatureValid)
            {
                Console.WriteLine("Cảnh báo: Webhook PayOS có chữ ký không hợp lệ!");
                return BadRequest(new { success = false, message = "Xác thực chữ ký thất bại." });
            }

            // 3. Phân tích gói tin thành JSON Object
            try
            {
                var payload = Newtonsoft.Json.Linq.JObject.Parse(requestBody);
                string code = payload["code"]?.ToString() ?? "";
                
                // code == "00" nghĩa là giao dịch thanh toán thành công
                if (code == "00")
                {
                    var data = payload["data"];
                    if (data != null)
                    {
                        // Lấy orderCode từ PayOS (Đây là phần số của OrderCode trong DB)
                        long payOsOrderId = (long?)data["orderCode"] ?? 0;

                        // Tìm đơn hàng trong database bằng cách so khớp đuôi của OrderCode (VD: SB-123456 chứa 123456)
                        var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderCode.EndsWith(payOsOrderId.ToString()));
                        if (order != null)
                        {
                            // Kiểm tra nếu đơn hàng chưa được hoàn thành thì cập nhật
                            if (order.Status != "Completed" || order.PaymentStatus != "Paid")
                            {
                                order.PaymentStatus = "Paid";
                                order.Status = "Completed"; // Cập nhật trạng thái thành 'Completed'
                                _context.Orders.Update(order);
                                await _context.SaveChangesAsync();
                                
                                Console.WriteLine($"Đơn hàng {order.OrderCode} đã được cập nhật thành 'Completed' tự động qua PayOS Webhook.");

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
                                        Console.WriteLine($"[Webhook] Lỗi gửi email ngầm PayOS: {ex.Message}");
                                    }
                                });
                            }
                        }
                    }
                }

                // Trả về kết quả thành công cho PayOS để họ xác nhận đã nhận webhook
                return Ok(new { success = true, message = "Nhận webhook thành công." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Webhook nhận kết quả chuyển khoản từ SePay (Lắng nghe SMS / Biến động số dư)
        /// Địa chỉ endpoint: POST /api/webhooks/sepay
        /// </summary>
        [HttpPost("sepay")]
        public async Task<IActionResult> ReceiveSePayWebhook()
        {
            // 1. Kiểm tra API Key cấu hình bảo mật từ SePay gửi trong Headers (để chống spam)
            string authHeader = Request.Headers["Authorization"].ToString() ?? "";
            
            // Thay thế "Apikey YOUR_SEPAY_API_KEY" bằng API Key thực tế của bạn cấu hình trên SePay Dashboard
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Apikey "))
            {
                if (_env.IsDevelopment())
                {
                    Console.WriteLine("[Webhook SePay] Cảnh báo: Bỏ qua xác thực API Key trong môi trường Development (Local).");
                }
                else
                {
                    return Unauthorized(new { success = false, message = "Thiếu API Key xác thực." });
                }
            }

            // 2. Đọc nội dung giao dịch chuyển khoản
            string requestBody;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                requestBody = await reader.ReadToEndAsync();
            }

            try
            {
                var sepayData = JsonConvert.DeserializeObject<SePayWebhookPayload>(requestBody);
                if (sepayData == null || sepayData.TransferType != "in") // Chỉ xử lý tiền vào (in)
                {
                    return Ok(new { success = false, message = "Chỉ xử lý giao dịch tiền vào." });
                }

                // 3. Trích xuất Mã đơn hàng từ nội dung chuyển khoản (Ví dụ khách chuyển với cú pháp: "SB-123456 thanh toan" hoặc "SB123456")
                string transactionContent = sepayData.Content ?? "";
                
                // Sử dụng Regex để quét mã đơn hàng có cấu trúc "SB-xxxxxx" hoặc "SBxxxxxx" (dấu gạch ngang là tùy chọn)
                var match = Regex.Match(transactionContent, @"SB-?\d{6}", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string matchedOrderCode = match.Value;

                    // Chuẩn hóa mã đơn hàng tìm thấy để so sánh (loại bỏ dấu gạch ngang và chuyển về chữ thường)
                    string normalizedMatched = matchedOrderCode.Replace("-", "").ToLower();

                    // Tìm đơn hàng tương ứng trong Database dựa trên OrderCode sau khi chuẩn hóa loại bỏ dấu gạch ngang
                    var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderCode.Replace("-", "").ToLower() == normalizedMatched);
                    if (order != null)
                    {
                        // Kiểm tra khớp số tiền chuyển khoản (Tùy chọn)
                        if (sepayData.TransferAmount >= order.TotalAmount)
                        {
                            if (order.Status != "Completed" || order.PaymentStatus != "Paid")
                            {
                                order.PaymentStatus = "Paid";
                                order.Status = "Completed"; // Cập nhật Database thành 'Completed'
                                _context.Orders.Update(order);
                                await _context.SaveChangesAsync();

                                Console.WriteLine($"Đơn hàng {order.OrderCode} đã được cập nhật thành 'Completed' tự động qua SePay Webhook.");

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
                                        Console.WriteLine($"[Webhook] Lỗi gửi email ngầm SePay: {ex.Message}");
                                    }
                                });
                            }
                            return Ok(new { success = true, message = "Cập nhật đơn hàng thành công." });
                        }
                        else
                        {
                            return Ok(new { success = false, message = "Số tiền chuyển khoản không khớp số tiền đơn hàng." });
                        }
                    }
                }

                return Ok(new { success = false, message = "Không tìm thấy mã đơn hàng hợp lệ trong nội dung chuyển khoản." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


    }

    #region SePay Webhook DTO

    public class SePayWebhookPayload
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("gateway")]
        public string Gateway { get; set; } = string.Empty; // Ngân hàng nhận (ví dụ MBBank)

        [JsonProperty("transactionDate")]
        public string TransactionDate { get; set; } = string.Empty;

        [JsonProperty("accountNumber")]
        public string AccountNumber { get; set; } = string.Empty;

        [JsonProperty("transferType")]
        public string TransferType { get; set; } = string.Empty; // "in" là tiền vào, "out" là tiền ra

        [JsonProperty("transferAmount")]
        public decimal TransferAmount { get; set; } // Số tiền chuyển khoản

        [JsonProperty("content")]
        public string Content { get; set; } = string.Empty; // Nội dung chuyển khoản chuyển đổi không dấu

        [JsonProperty("referenceCode")]
        public string ReferenceCode { get; set; } = string.Empty; // Mã tham chiếu của ngân hàng
    }

    #endregion
}
