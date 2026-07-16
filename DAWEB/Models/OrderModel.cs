using System;
using System.ComponentModel.DataAnnotations;

namespace DAWEB.Models
{
    public class OrderModel
    {
        [Key]
        public int Id { get; set; }
        
        public string OrderCode { get; set; } = string.Empty;
        
        public string UserName { get; set; } = string.Empty; // Liên kết với Email/Tên tài khoản đặt bánh
        
        [Required(ErrorMessage = "Vui lòng nhập họ tên người nhận")]
        public string ReceiverName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string ReceiverPhone { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng")]
        public string ReceiverAddress { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
        public string PaymentMethod { get; set; } = string.Empty;
        
        public decimal TotalAmount { get; set; }
        
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Pending"; // Trạng thái đơn hàng: Pending, Completed, Cancelled
        
        public string PaymentStatus { get; set; } = "Unpaid"; // Trạng thái thanh toán: Unpaid, Paid

        public string? VoucherCode { get; set; }

        public decimal DiscountAmount { get; set; } = 0;

        public string DeliveryMethod { get; set; } = "Giao tận nơi"; // "Giao tận nơi" hoặc "Đến lấy trực tiếp"

        public decimal ShippingFee { get; set; } = 0;
    }
}
