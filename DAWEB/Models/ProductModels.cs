using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAWEB.Models
{
    public class ProductModels
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [StringLength(200)]
        public string ProductName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả sản phẩm")]
        [StringLength(200)]
        public string ProductDescription { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập giá tiền sản phẩm")]
        [Range(0.01, double.MaxValue)]
        public decimal ProductPrice { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập màu sắc sản phẩm")]
        [StringLength(200)]
        public string ProductColor { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập kích thước sản phẩm")]
        [StringLength(20)]
        public string ProductSize { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập chất liệu sản phẩm")]
        [StringLength(200)]
        public string ProductMaterial { get; set; } = string.Empty;

        [StringLength(200)]
        public string? ProductGender { get; set; } = "Không phân loại";

 
        [Range(0, int.MaxValue, ErrorMessage = "Số lượng lớn hơn hoặc bằng 0" )]
        public int ProductQuantity { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập loại sản phẩm")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual CategoryModel? Category { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập hình ảnh sản phẩm")]
        public string ImageUrl { get; set; } = string.Empty;

        [DataType(DataType.DateTime)]
        public DateTime CreateDate { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime UpdateDate { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}
