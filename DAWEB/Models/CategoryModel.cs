using System.ComponentModel.DataAnnotations;

namespace DAWEB.Models
{
    public class CategoryModel
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập slug danh mục (dùng cho URL)")]
        [StringLength(100)]
        public string Slug { get; set; } = string.Empty;

        [StringLength(250)]
        public string Description { get; set; } = string.Empty;
    }
}
