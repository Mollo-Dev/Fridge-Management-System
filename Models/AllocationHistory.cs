using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace GRP_03_27.Models
{
    public class AllocationHistory
    {
        [Key]
        public int AllocationHistoryId { get; set; }

        [Required]
        [Display(Name = "Fridge")]
        public int FridgeId { get; set; }

        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Required]
        [Display(Name = "Action")]
        public AllocationAction Action { get; set; }

        [Required]
        [Display(Name = "Action Date")]
        public DateTime ActionDate { get; set; } = DateTime.UtcNow;

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string Notes { get; set; }

        [Required]
        [Display(Name = "Action By")]
        public string ActionById { get; set; }

        // Navigation properties
        public virtual Fridge Fridge { get; set; }
        public virtual Customer Customer { get; set; }
        public virtual User ActionBy { get; set; }

        // Computed properties - ADD [NotMapped]
        [Display(Name = "Action Description")]
        [NotMapped]
        public string ActionDescription => Action switch
        {
            AllocationAction.Allocated => $"Allocated to {Customer?.BusinessName ?? "Unknown Customer"}",
            AllocationAction.Deallocated => "Deallocated from customer",
            AllocationAction.Scrapped => "Marked as scrapped",
            AllocationAction.Received => "Received from supplier",
            _ => Action.ToString()
        };
    }
}