using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DAWEB.Models
{
    public class OrderDetailModel
    {
        [Key]
        public int Id { get; set; }
        
        public int OrderId { get; set; }
        
        public int ProductId { get; set; }
        
        public string ProductName { get; set; } = string.Empty;
        
        public decimal Price { get; set; }
        
        public int Quantity { get; set; }
        
        [ForeignKey("OrderId")]
        public virtual OrderModel? Order { get; set; }
        
        [ForeignKey("ProductId")]
        public virtual ProductModels? Product { get; set; }
    }
}
