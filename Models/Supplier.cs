using System.ComponentModel.DataAnnotations;

namespace GRP_03_27.Models
{
    public class Supplier
    {
        [Key]
        public int SupplierId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [StringLength(15)]
        [Phone]
        public string ContactNumber { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        [StringLength(500)]
        public string Address { get; set; }

        // Navigation properties - CORRECTED
        public virtual ICollection<Fridge> Fridges { get; set; }
        public virtual ICollection<PurchaseRequest> PurchaseRequests { get; set; } // Fixed plural
    }
}