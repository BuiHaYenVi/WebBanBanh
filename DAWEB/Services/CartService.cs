using DAWEB.Models;
using DAWEB.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DAWEB.Services
{
    public class CartService
    {
        private readonly DataContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartService(DataContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// Lấy UserId (UserName) của người dùng hiện tại. Trả về null nếu chưa đăng nhập.
        /// </summary>
        private string? GetCurrentUserId()
        {
            return _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        }

        /// <summary>
        /// Lấy danh sách sản phẩm trong giỏ hàng của user hiện tại từ Database.
        /// </summary>
        public List<CartItem> GetCartItems()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return new List<CartItem>();

            return _context.Carts.Where(c => c.UserId == userId).ToList();
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng sau khi đặt hàng thành công.
        /// </summary>
        public void ClearCart()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return;

            var userCartItems = _context.Carts.Where(c => c.UserId == userId);
            _context.Carts.RemoveRange(userCartItems);
            _context.SaveChanges();
        }

        /// <summary>
        /// Thêm sản phẩm vào giỏ hàng (lưu vào Database).
        /// </summary>
        public (bool Success, string Message) AddToCart(int productId, int quantity)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Vui lòng đăng nhập để thêm sản phẩm vào giỏ hàng.");
            }

            var product = _context.Products.FirstOrDefault(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return (false, "Sản phẩm không tồn tại hoặc đã ngừng kinh doanh.");
            }

            var existingItem = _context.Carts.FirstOrDefault(c => c.UserId == userId && c.ProductId == productId);

            if (existingItem != null)
            {
                if (existingItem.Quantity + quantity > product.ProductQuantity)
                {
                    return (false, $"Không thể thêm. Số lượng trong giỏ hàng đã đạt giới hạn tồn kho ({product.ProductQuantity}).");
                }
                existingItem.Quantity += quantity;
                _context.Carts.Update(existingItem);
            }
            else
            {
                if (quantity > product.ProductQuantity)
                {
                    return (false, $"Số lượng vượt quá tồn kho (Tối đa: {product.ProductQuantity}).");
                }
                var cartItem = new CartItem
                {
                    UserId = userId,
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    Price = product.ProductPrice,
                    Quantity = quantity
                };
                _context.Carts.Add(cartItem);
            }

            _context.SaveChanges();
            return (true, "Đã thêm sản phẩm vào giỏ hàng thành công!");
        }

        /// <summary>
        /// Cập nhật số lượng sản phẩm trong giỏ hàng.
        /// </summary>
        public (bool Success, string Message, int MaxQuantity) UpdateQuantity(int productId, int quantity)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return (false, "Vui lòng đăng nhập.", 0);
            }

            var item = _context.Carts.FirstOrDefault(c => c.UserId == userId && c.ProductId == productId);
            if (item == null)
            {
                return (false, "Không tìm thấy sản phẩm trong giỏ hàng.", 0);
            }

            var product = _context.Products.FirstOrDefault(p => p.Id == productId && !p.IsDeleted);
            if (product == null)
            {
                return (false, "Sản phẩm không tồn tại hoặc đã ngừng kinh doanh.", 0);
            }

            if (quantity > product.ProductQuantity)
            {
                return (false, $"Số lượng vượt quá tồn kho (Tối đa: {product.ProductQuantity}).", product.ProductQuantity);
            }

            if (quantity <= 0)
            {
                return (false, "Số lượng tối thiểu là 1.", 0);
            }

            item.Quantity = quantity;
            _context.Carts.Update(item);
            _context.SaveChanges();

            return (true, "Cập nhật thành công", 0);
        }

        /// <summary>
        /// Xóa 1 sản phẩm khỏi giỏ hàng.
        /// </summary>
        public void RemoveFromCart(int productId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return;

            var item = _context.Carts.FirstOrDefault(c => c.UserId == userId && c.ProductId == productId);
            if (item != null)
            {
                _context.Carts.Remove(item);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Tính tổng tiền giỏ hàng.
        /// </summary>
        public decimal GetTotalAmount()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return 0;

            return _context.Carts.Where(c => c.UserId == userId).Sum(c => c.Price * c.Quantity);
        }

        /// <summary>
        /// Đếm tổng số lượng sản phẩm trong giỏ.
        /// </summary>
        public int GetCartCount()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return 0;

            return _context.Carts.Where(c => c.UserId == userId).Sum(c => c.Quantity);
        }
    }
}
