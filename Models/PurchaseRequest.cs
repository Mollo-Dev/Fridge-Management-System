using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace GRP_03_27.Models
{
    public class PurchaseRequest
    {
        [Key]
        public int PurchaseRequestId { get; set; }

        [Required]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Quantity is required")]
        [Range(1, 100, ErrorMessage = "Quantity must be between 1 and 100")]
        public int Quantity { get; set; }

        [Required(ErrorMessage = "Reason is required")]
        [StringLength(500, ErrorMessage = "Reason cannot exceed 500 characters")]
        public string Reason { get; set; }

        [Required]
        [Display(Name = "Priority Level")]
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        [Required]
        public PurchaseRequestStatus Status { get; set; } = PurchaseRequestStatus.Pending;

        [Display(Name = "Estimated Cost")]
        [Range(0, 1000000, ErrorMessage = "Estimated cost must be reasonable")]
        public decimal? EstimatedCost { get; set; }

        [Display(Name = "Urgent Justification")]
        [StringLength(1000, ErrorMessage = "Justification cannot exceed 1000 characters")]
        public string UrgentJustification { get; set; }

        // Foreign keys
        [Required]
        [Display(Name = "Requested By")]
        public string RequestedById { get; set; }

        // Navigation properties
        public virtual User RequestedBy { get; set; }

        // Computed properties - ADD [NotMapped]
        [Display(Name = "Days Pending")]
        [NotMapped]
        public int DaysPending => (int)(DateTime.UtcNow - RequestDate).TotalDays;

        [Display(Name = "Is Overdue")]
        [NotMapped]
        public bool IsOverdue => DaysPending > 7 && Status == PurchaseRequestStatus.Pending;
    }
}