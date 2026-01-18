using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GRP_03_27.Models
{
    public class FaultReport
    {
        [Key]
        public int FaultReportId { get; set; }

        [Required]
        [Display(Name = "Date Reported")]
        public DateTime DateReported { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Fault description is required")]
        [StringLength(1000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 1000 characters")]
        [Display(Name = "Fault Description")]
        public string Description { get; set; }

        [Required]
        [Display(Name = "Status")]
        public FaultStatus Status { get; set; } = FaultStatus.Reported;

        // ADD THIS: Make Priority a stored property
        [Required]
        [Display(Name = "Priority Level")]
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        [StringLength(2000, ErrorMessage = "Diagnosis details cannot exceed 2000 characters")]
        [Display(Name = "Diagnosis Details")]
        public string? DiagnosisDetails { get; set; }

        [Display(Name = "Estimated Repair Date")]
        [DataType(DataType.Date)]
        public DateTime? ScheduledDate { get; set; }

        [Display(Name = "Actual Repair Date")]
        [DataType(DataType.Date)]
        public DateTime? RepairDate { get; set; }

        [Display(Name = "Repair Cost")]
        [Range(0, 100000, ErrorMessage = "Repair cost must be between R0 and R100,000")]
        public decimal? RepairCost { get; set; }

        [Display(Name = "Parts Required")]
        [StringLength(500, ErrorMessage = "Parts description cannot exceed 500 characters")]
        public string? PartsRequired { get; set; }

        [Display(Name = "Customer Notes")]
        [StringLength(1000, ErrorMessage = "Customer notes cannot exceed 1000 characters")]
        public string? CustomerNotes { get; set; }

        [Display(Name = "Internal Notes")]
        [StringLength(1000, ErrorMessage = "Internal notes cannot exceed 1000 characters")]
        public string? InternalNotes { get; set; }

        [Display(Name = "Request Replacement Fridge")]
        public bool RequestReplacement { get; set; }

        [Display(Name = "Replacement Approved")]
        public bool? ReplacementApproved { get; set; }

        // Foreign keys
        [Display(Name = "Fridge")]
        public int? FridgeId { get; set; }

        [Required]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Display(Name = "Assigned Technician")]
        public string? AssignedTechnicianId { get; set; }

        // Navigation properties
        public virtual Fridge? Fridge { get; set; }
        public virtual Customer Customer { get; set; }
        public virtual User? AssignedTechnician { get; set; }

        // Computed properties (add [NotMapped] attribute)
        [Display(Name = "Days Since Report")]
        [NotMapped]
        public int DaysSinceReport => (int)(DateTime.UtcNow - DateReported).TotalDays;

        [Display(Name = "Is Overdue")]
        [NotMapped]
        public bool IsOverdue => DaysSinceReport > 7 &&
            (Status == FaultStatus.Reported || Status == FaultStatus.Diagnosed);

        // Business rule validation method
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (RepairDate.HasValue && RepairDate.Value > DateTime.UtcNow)
            {
                yield return new ValidationResult(
                    "Repair date cannot be in the future.",
                    new[] { nameof(RepairDate) });
            }

            if (ScheduledDate.HasValue && ScheduledDate.Value < DateTime.UtcNow.Date)
            {
                yield return new ValidationResult(
                    "Scheduled date must be today or in the future.",
                    new[] { nameof(ScheduledDate) });
            }

            if (Status == FaultStatus.Resolved && !RepairDate.HasValue)
            {
                yield return new ValidationResult(
                    "Repair date is required when status is set to Resolved.",
                    new[] { nameof(RepairDate) });
            }
        }
    }
}