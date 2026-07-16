using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace DAWEB.Services.PayOS
{
    /// <summary>
    /// Service tương tác với cổng thanh toán PayOS (Tạo link thanh toán và Xác thực Webhook)
    /// </summary>
    public class PayOSService
    {
        private readonly string _clientId;
        private readonly string _apiKey;
        private readonly string _checksumKey;

        public PayOSService(IConfiguration configuration)
        {
            // Đọc thông số cấu hình từ appsettings.json
            _clientId = configuration["PayOS:ClientId"] ?? "";
            _apiKey = configuration["PayOS:ApiKey"] ?? "";
            _checksumKey = configuration["PayOS:ChecksumKey"] ?? "";
        }

        /// <summary>
        /// Tạo link thanh toán PayOS
        /// </summary>
        /// <param name="orderId">Mã đơn hàng (Id dạng số trong database)</param>
        /// <param name="amount">Số tiền thanh toán</param>
        /// <param name="description">Nội dung thanh toán ngắn gọn (Không dấu, không ký tự đặc biệt)</param>
        /// <param name="cancelUrl">Đường dẫn khi khách hủy thanh toán</param>
        /// <param name="returnUrl">Đường dẫn khi khách thanh toán thành công và quay lại website</param>
        public async Task<PayOSPaymentResponse?> CreatePaymentLinkAsync(long orderId, decimal amount, string description, string cancelUrl, string returnUrl)
        {
            int intAmount = (int)amount;

            // 1. Tạo chuỗi ký tự thô để ký (Sắp xếp theo thứ tự bảng chữ cái alphabet của key)
            // Cú pháp: amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}
            string rawData = $"amount={intAmount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderId}&returnUrl={returnUrl}";
            
            // 2. Tính toán chữ ký bảo mật signature bằng ChecksumKey
            string signature = GenerateSignature(rawData, _checksumKey);

            // 3. Xây dựng Request Body gửi sang PayOS
            var requestBody = new
            {
                orderCode = orderId,
                amount = intAmount,
                description = description,
                cancelUrl = cancelUrl,
                returnUrl = returnUrl,
                signature = signature
            };

            // 4. Gửi HTTP POST tới API PayOS
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("x-client-id", _clientId);
                client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

                string jsonPayload = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync("https://api-merchant.payos.vn/v2/payment-requests", content);
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Lỗi từ PayOS API: {responseContent}");
                        return null;
                    }

                    var apiResponse = JsonConvert.DeserializeObject<PayOSApiResponse>(responseContent);
                    if (apiResponse != null && apiResponse.Code == "00")
                    {
                        return apiResponse.Data;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi kết nối PayOS: {ex.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Lấy thông tin thanh toán của đơn hàng từ PayOS
        /// </summary>
        /// <param name="orderId">Mã đơn hàng (Id dạng số trong database)</param>
        public async Task<string?> GetPaymentStatusAsync(long orderId)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("x-client-id", _clientId);
                client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

                try
                {
                    var response = await client.GetAsync($"https://api-merchant.payos.vn/v2/payment-requests/{orderId}");
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Lỗi từ PayOS GetPaymentStatus API: {responseContent}");
                        return null;
                    }

                    var apiResponse = JsonConvert.DeserializeObject<PayOSApiResponse>(responseContent);
                    if (apiResponse != null && apiResponse.Code == "00" && apiResponse.Data != null)
                    {
                        return apiResponse.Data.Status; // Trả về status như PAID, PENDING, CANCELLED
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi kết nối PayOS khi check status: {ex.Message}");
                }

                return null;
            }
        }

        /// <summary>
        /// Lấy chi tiết thông tin thanh toán của đơn hàng từ PayOS
        /// </summary>
        /// <param name="orderId">Mã đơn hàng (Id dạng số trong database)</param>
        public async Task<PayOSPaymentResponse?> GetPaymentLinkDetailsAsync(long orderId)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("x-client-id", _clientId);
                client.DefaultRequestHeaders.Add("x-api-key", _apiKey);

                try
                {
                    var response = await client.GetAsync($"https://api-merchant.payos.vn/v2/payment-requests/{orderId}");
                    string responseContent = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Lỗi từ PayOS GetPaymentLinkDetails API: {responseContent}");
                        return null;
                    }

                    var apiResponse = JsonConvert.DeserializeObject<PayOSApiResponse>(responseContent);
                    if (apiResponse != null && apiResponse.Code == "00")
                    {
                        return apiResponse.Data;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi kết nối PayOS khi lấy chi tiết: {ex.Message}");
                }

                return null;
            }
        }


        /// <summary>
        /// Xác minh dữ liệu nhận được từ Webhook PayOS
        /// </summary>
        /// <param name="webhookRawJson">Dữ liệu thô JSON nhận được từ Webhook POST request</param>
        /// <returns>True nếu chữ ký hợp lệ và dữ liệu toàn vẹn</returns>
        public bool VerifyWebhookData(string webhookRawJson)
        {
            try
            {
                // Phân tích JSON thô nhận được từ PayOS
                var payload = Newtonsoft.Json.Linq.JObject.Parse(webhookRawJson);
                var data = payload["data"];
                if (data == null) return false;

                // Lấy chữ ký gửi kèm trong gói payload
                string signatureFromWebhook = payload["signature"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(signatureFromWebhook)) return false;

                // Lấy các tham số cần thiết để tự tạo lại chữ ký đối chứng
                int amount = (int?)data["amount"] ?? 0;
                string description = data["description"]?.ToString() ?? "";
                long orderCode = (long?)data["orderCode"] ?? 0;
                string paymentLinkId = data["paymentLinkId"]?.ToString() ?? "";
                string reference = data["reference"]?.ToString() ?? "";
                string transactionDateTime = data["transactionDateTime"]?.ToString() ?? "";

                // Tạo lại chuỗi ký thô (Sắp xếp theo thứ tự alphabet của key)
                // Cú pháp: amount={amount}&description={description}&orderCode={orderCode}&paymentLinkId={paymentLinkId}&reference={reference}&transactionDateTime={transactionDateTime}
                string rawData = $"amount={amount}&description={description}&orderCode={orderCode}&paymentLinkId={paymentLinkId}&reference={reference}&transactionDateTime={transactionDateTime}";

                // Tính chữ ký cục bộ
                string expectedSignature = GenerateSignature(rawData, _checksumKey);

                // So sánh hai chữ ký (không phân biệt hoa thường)
                return string.Equals(signatureFromWebhook, expectedSignature, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi kiểm tra Webhook: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Hàm băm HMAC-SHA256 chuyển đổi thành chuỗi Hex viết thường
        /// </summary>
        private static string GenerateSignature(string data, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);

            using (var hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(dataBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }
    }

    #region Các Lớp Dữ Liệu PayOS DTOs

    public class PayOSApiResponse
    {
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;

        [JsonProperty("desc")]
        public string Desc { get; set; } = string.Empty;

        [JsonProperty("data")]
        public PayOSPaymentResponse? Data { get; set; }
    }

    public class PayOSPaymentResponse
    {
        [JsonProperty("bin")]
        public string Bin { get; set; } = string.Empty;

        [JsonProperty("accountNumber")]
        public string AccountNumber { get; set; } = string.Empty;

        [JsonProperty("accountName")]
        public string AccountName { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("orderCode")]
        public long OrderCode { get; set; }

        [JsonProperty("paymentLinkId")]
        public string PaymentLinkId { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("checkoutUrl")]
        public string CheckoutUrl { get; set; } = string.Empty;

        [JsonProperty("qrCode")]
        public string QrCode { get; set; } = string.Empty;
    }

    #endregion
}
