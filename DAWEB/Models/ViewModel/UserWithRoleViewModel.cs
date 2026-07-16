namespace DAWEB.Models.ViewModel
{
    public class UserWithRoleViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string RoleNames { get; set; } = string.Empty;
        public bool IsLockedOut { get; set; }
    }
}
