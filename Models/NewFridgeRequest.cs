using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GRP_03_27.Models
{
    public class NewFridgeRequest
    {
        [Key]
        public int NewFridgeRequestId { get; set; }

        [Required]
        [Display(Name = "Request Date")]
        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Range(1, 50, ErrorMessage = "Quantity must be between 1 and 50")]
        [Display(Name = "Quantity Requested")]
        public int Quantity { get; set; }

        [Required]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Business justification must be between 10 and 1000 characters")]
        [Display(Name = "Business Justification")]
        public string BusinessJustification { get; set; }

        [StringLength(500, ErrorMessage = "Additional notes cannot exceed 500 characters")]
        [Display(Name = "Additional Notes")]
        public string? AdditionalNotes { get; set; }

        [Required]
        [Display(Name = "Status")]
        public NewFridgeRequestStatus Status { get; set; } = NewFridgeRequestStatus.Pending;

        [Display(Name = "Priority")]
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        [Display(Name = "Estimated Quantity")]
        public int? EstimatedQuantity { get; set; }

        [Display(Name = "Approved Quantity")]
        public int? ApprovedQuantity { get; set; }

        [Display(Name = "Approval Date")]
        public DateTime? ApprovalDate { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedById { get; set; }

        [StringLength(1000, ErrorMessage = "Approval notes cannot exceed 1000 characters")]
        [Display(Name = "Approval Notes")]
        public string? ApprovalNotes { get; set; }

        [Display(Name = "Allocation Date")]
        public DateTime? AllocationDate { get; set; }

        [Display(Name = "Allocated By")]
        public string? AllocatedById { get; set; }

        [StringLength(1000, ErrorMessage = "Allocation notes cannot exceed 1000 characters")]
        [Display(Name = "Allocation Notes")]
        public string? AllocationNotes { get; set; }

        // Foreign keys
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Display(Name = "Requested By")]
        public string? RequestedById { get; set; }

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        // Note: User navigation properties removed to avoid cascade path conflicts

        // Computed properties
        [Display(Name = "Days Since Request")]
        public int DaysSinceRequest => (int)(DateTime.UtcNow - RequestDate).TotalDays;

        [Display(Name = "Is Overdue")]
        public bool IsOverdue => DaysSinceRequest > 7 && Status == NewFridgeRequestStatus.Pending;
    }
}
