using System.ComponentModel.DataAnnotations;

namespace DAWEB.Models.ViewModel
{
    public class UserEditViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới (nếu muốn đổi)")]
        public string NewPassword { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}
