using Microsoft.AspNetCore.Mvc;
using DAWEB.Helpers.VietQR;

namespace DAWEB.Controllers
{
    /// <summary>
    /// Controller demo hướng dẫn tạo và hiển thị mã VietQR
    /// </summary>
    public class VietQRDemoController : Controller
    {
        // Admin cũng được phép truy cập
        /// <summary>
        /// Trang hiển thị mã QR thanh toán dựa trên số tiền và mã đơn hàng truyền vào
        /// </summary>
        /// <param name="total_amount">Số tiền của đơn hàng (ví dụ: 150000)</param>
        /// <param name="order_code">Mã đơn hàng dùng làm nội dung chuyển khoản (ví dụ: DH8973)</param>
        public IActionResult Index(decimal total_amount = 50000, string order_code = "DH123456")
        {
            // 1. Thông tin cấu hình tài khoản MB Bank thụ hưởng theo yêu cầu
            string bankBin = "970422"; // MB Bank (Mã BIN)
            string accountNumber = "0933436589";
            string accountName = "BUI HA YEN VI";

            // 2. Tạo chuỗi VietQR chuẩn EMVCo từ Helper
            // Hàm này tự động sinh chuỗi TLV đầy đủ và tính checksum CRC-16
            string vietQrString = VietQRHelper.GenerateVietQR(
                bankBin: bankBin, 
                accountNumber: accountNumber, 
                accountName: accountName, 
                amount: total_amount, 
                orderCode: order_code
            );

            // 3. Đưa các thông tin vào ViewBag để hiển thị trên Razor View (Frontend)
            ViewBag.BankBin = bankBin;
            ViewBag.BankName = "MB Bank (Ngân hàng TMCP Quân Đội)";
            ViewBag.AccountNumber = accountNumber;
            ViewBag.AccountName = accountName;
            ViewBag.TotalAmount = total_amount;
            ViewBag.OrderCode = order_code;
            ViewBag.VietQrString = vietQrString;

            // 4. Trả về View tương ứng (DAWEB/Views/VietQRDemo/Index.cshtml)
            return View();
        }
    }
}
