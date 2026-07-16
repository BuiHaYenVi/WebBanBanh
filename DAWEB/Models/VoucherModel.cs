using System;
using System.ComponentModel.DataAnnotations;

namespace DAWEB.Models
{
    public class VoucherModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã Voucher")]
        [StringLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn loại giảm giá")]
        public string DiscountType { get; set; } = "Fixed"; // "Fixed" hoặc "Percent"

        [Required(ErrorMessage = "Vui lòng nhập giá trị giảm")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá trị giảm phải lớn hơn 0")]
        public decimal DiscountValue { get; set; }

        public decimal? MaxDiscountAmount { get; set; }

        [Range(0, double.MaxValue)]
        public decimal MinimumOrderAmount { get; set; } = 0;

        [Required]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [Required]
        public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7);

        [Range(1, int.MaxValue, ErrorMessage = "Giới hạn sử dụng phải lớn hơn 0")]
        public int UsageLimit { get; set; } = 1;

        public int UsedCount { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }
}
