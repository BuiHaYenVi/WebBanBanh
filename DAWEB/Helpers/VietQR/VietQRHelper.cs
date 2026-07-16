using System;
using System.Globalization;
using System.Text;

namespace DAWEB.Helpers.VietQR
{
    /// <summary>
    /// Lớp tiện ích hỗ trợ tạo chuỗi thanh toán chuẩn VietQR (EMVCo)
    /// </summary>
    public static class VietQRHelper
    {
        // Các Tag ID chuẩn EMVCo / VietQR
        private const string TAG_PAYLOAD_FORMAT = "00";      // Payload Format Indicator (Cố định: "01")
        private const string TAG_INITIATION_METHOD = "01";   // Point of Initiation Method ("11": Tĩnh, "12": Động)
        private const string TAG_MERCHANT_INFO = "38";       // Merchant Account Information (NAPAS)
        private const string TAG_CURRENCY = "53";            // Transaction Currency (Mã VND: "704")
        private const string TAG_AMOUNT = "54";              // Transaction Amount (Số tiền)
        private const string TAG_COUNTRY_CODE = "58";        // Country Code (Mã quốc gia: "VN")
        private const string TAG_MERCHANT_NAME = "59";        // Merchant Name (Tên chủ tài khoản - viết hoa không dấu)
        private const string TAG_ADDITIONAL_DATA = "62";     // Additional Data Field (Thông tin thêm: Nội dung ck)
        private const string TAG_CRC = "63";                 // Cyclic Redundancy Check (Mã kiểm tra lỗi)

        // Các Tag con của Merchant Information (Tag 38)
        private const string SUBTAG_GUID = "00";             // NAPAS AID (Cố định: "A000000727")
        private const string SUBTAG_BENEFICIARY = "01";      // Thông tin người nhận
        private const string SUBTAG_SERVICE_CODE = "02";     // Mã dịch vụ (Cố định: "QRIBFTTA" - chuyển khoản đến TK)

        // Các Tag con của Beneficiary Info (Sub-tag 01 của Tag 38)
        private const string SUBSUBTAG_BANK_BIN = "00";      // Mã BIN ngân hàng (Ví dụ MB Bank: "970422")
        private const string SUBSUBTAG_ACCOUNT_NO = "01";    // Số tài khoản nhận tiền

        // Các Tag con của Additional Data (Tag 62)
        private const string SUBTAG_PURPOSE = "08";          // Nội dung chuyển khoản

        /// <summary>
        /// Hàm tạo chuỗi VietQR chuẩn EMVCo từ các thông tin thanh toán.
        /// </summary>
        /// <param name="bankBin">Mã BIN ngân hàng thụ hưởng (ví dụ MB: 970422)</param>
        /// <param name="accountNumber">Số tài khoản thụ hưởng</param>
        /// <param name="accountName">Tên chủ tài khoản (sẽ được tự động chuẩn hóa viết hoa không dấu)</param>
        /// <param name="amount">Số tiền thanh toán (bắt buộc lớn hơn 0 đối với QR động)</param>
        /// <param name="orderCode">Nội dung chuyển khoản (Mã đơn hàng, viết hoa không dấu và ký tự đặc biệt)</param>
        /// <returns>Chuỗi VietQR thô chuẩn EMVCo</returns>
        public static string GenerateVietQR(string bankBin, string accountNumber, string accountName, decimal amount, string orderCode)
        {
            var qrBuilder = new StringBuilder();

            // 1. Payload Format Indicator (Tag 00): Giá trị mặc định là "01"
            qrBuilder.Append(BuildTlv(TAG_PAYLOAD_FORMAT, "01"));

            // 2. Point of Initiation Method (Tag 01): "12" cho QR động (có số tiền thay đổi)
            qrBuilder.Append(BuildTlv(TAG_INITIATION_METHOD, "12"));

            // 3. Merchant Account Information (Tag 38)
            // 3.1. Tạo thông tin thụ hưởng (Sub-sub-tags của Subtag 01)
            string bankBinTlv = BuildTlv(SUBSUBTAG_BANK_BIN, bankBin);
            string accountNoTlv = BuildTlv(SUBSUBTAG_ACCOUNT_NO, accountNumber);
            string beneficiaryValue = bankBinTlv + accountNoTlv;

            // 3.2. Tạo các Sub-tags của Tag 38
            string guidTlv = BuildTlv(SUBTAG_GUID, "A000000727"); // NAPAS AID
            string beneficiaryTlv = BuildTlv(SUBTAG_BENEFICIARY, beneficiaryValue);
            string serviceTlv = BuildTlv(SUBTAG_SERVICE_CODE, "QRIBFTTA"); // Chuyển nhanh qua tài khoản

            // 3.3. Gộp các thành phần của Tag 38
            string merchantInfoValue = guidTlv + beneficiaryTlv + serviceTlv;
            qrBuilder.Append(BuildTlv(TAG_MERCHANT_INFO, merchantInfoValue));

            // 4. Currency (Tag 53): Mã tiền tệ quốc gia Việt Nam VND là "704"
            qrBuilder.Append(BuildTlv(TAG_CURRENCY, "704"));

            // 5. Transaction Amount (Tag 54): Số tiền (làm tròn số nguyên vì VND không dùng phần thập phân)
            if (amount > 0)
            {
                string amountStr = ((long)amount).ToString();
                qrBuilder.Append(BuildTlv(TAG_AMOUNT, amountStr));
            }

            // 6. Country Code (Tag 58): Mã quốc gia Việt Nam là "VN"
            qrBuilder.Append(BuildTlv(TAG_COUNTRY_CODE, "VN"));

            // 7. Merchant Name (Tag 59): Tên người nhận (phải viết hoa, không dấu)
            string normalizedName = RemoveDiacritics(accountName).ToUpper();
            qrBuilder.Append(BuildTlv(TAG_MERCHANT_NAME, normalizedName));

            // 8. Additional Data (Tag 62)
            if (!string.IsNullOrEmpty(orderCode))
            {
                // Chuẩn hóa nội dung chuyển khoản không dấu
                string normalizedOrderCode = RemoveDiacritics(orderCode).ToUpper();
                string purposeTlv = BuildTlv(SUBTAG_PURPOSE, normalizedOrderCode);
                qrBuilder.Append(BuildTlv(TAG_ADDITIONAL_DATA, purposeTlv));
            }

            // 9. CRC Checksum (Tag 63)
            // Chuỗi để tính CRC: Chuỗi hiện tại nối thêm với "6304" (Tag 63 + Độ dài mã CRC là 04 ký tự)
            string preCrcString = qrBuilder.ToString() + TAG_CRC + "04";
            string crcValue = CalculateCrc16(preCrcString);

            // Gộp mã CRC hoàn thiện chuỗi VietQR
            qrBuilder.Append(TAG_CRC + "04" + crcValue);

            return qrBuilder.ToString();
        }

        /// <summary>
        /// Tạo một phân đoạn TLV (Tag-Length-Value)
        /// </summary>
        public static string BuildTlv(string tag, string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            string length = value.Length.ToString("D2"); // Format độ dài thành 2 chữ số (ví dụ: "05", "12")
            return $"{tag}{length}{value}";
        }

        /// <summary>
        /// Tính mã kiểm định CRC-16 CCITT (False) theo chuẩn EMVCo
        /// </summary>
        public static string CalculateCrc16(string input)
        {
            ushort crc = 0xFFFF; // Giá trị khởi tạo mặc định là 0xFFFF
            ushort polynomial = 0x1021; // Đa thức x^16 + x^12 + x^5 + 1
            byte[] bytes = Encoding.UTF8.GetBytes(input);

            foreach (byte b in bytes)
            {
                crc ^= (ushort)(b << 8);
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ polynomial);
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
            }

            return crc.ToString("X4"); // Định dạng hexa viết hoa có 4 ký tự (ví dụ: "A1B2")
        }

        /// <summary>
        /// Loại bỏ các dấu tiếng Việt và ký tự đặc biệt không được chấp nhận trong EMVCo
        /// </summary>
        public static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            // Chuyển ký tự Đ/đ riêng vì FormD không tự động tách được ký tự này
            text = text.Replace("đ", "d").Replace("Đ", "D");

            string normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    // Giữ lại các ký tự chữ, số và khoảng trắng thông thường
                    if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '.')
                    {
                        stringBuilder.Append(c);
                    }
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
