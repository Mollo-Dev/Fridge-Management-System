using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace GRP_03_27.Models
{
    public class MaintenanceRecord
    {
        [Key]
        public int MaintenanceRecordId { get; set; }

        [Required(ErrorMessage = "Scheduled date is required")]
        [Display(Name = "Scheduled Date")]
        [DataType(DataType.DateTime)]
        public DateTime ScheduledDate { get; set; }

        [Display(Name = "Performed Date")]
        [DataType(DataType.DateTime)]
        public DateTime? PerformedDate { get; set; }

        [Required(ErrorMessage = "Technician notes are required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Technician notes must be between 10 and 500 characters")]
        [Display(Name = "Technician Notes")]
        public string TechnicianNotes { get; set; }

        [Required(ErrorMessage = "Service checklist is required")]
        [StringLength(1000, MinimumLength = 20, ErrorMessage = "Service checklist must be between 20 and 1000 characters")]
        [Display(Name = "Service Checklist")]
        public string ServiceChecklist { get; set; }

        [Required(ErrorMessage = "Maintenance type is required")]
        [Display(Name = "Maintenance Type")]
        public MaintenanceType MaintenanceType { get; set; }

        // Status tracking
        [Display(Name = "Maintenance Status")]
        public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Scheduled;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [Display(Name = "Priority Level")]
        public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

        [Display(Name = "Estimated Duration (hours)")]
        [Range(0.5, 24, ErrorMessage = "Duration must be between 0.5 and 24 hours")]
        public double EstimatedDuration { get; set; } = 1.0;

        [Display(Name = "Actual Duration (hours)")]
        [Range(0, 24, ErrorMessage = "Duration must be between 0 and 24 hours")]
        public double? ActualDuration { get; set; }

        [Display(Name = "Parts Used")]
        [StringLength(500, ErrorMessage = "Parts description cannot exceed 500 characters")]
        public string? PartsUsed { get; set; }

        [Display(Name = "Total Cost")]
        [Range(0, 10000, ErrorMessage = "Cost must be between 0 and 10,000")]
        public decimal TotalCost { get; set; }

        // Foreign keys
        [Required(ErrorMessage = "Fridge is required")]
        public int FridgeId { get; set; }

        [Required(ErrorMessage = "Technician is required")]
        public string AssignedTechnicianId { get; set; }

        // Navigation properties
        public virtual Fridge Fridge { get; set; }
        public virtual User AssignedTechnician { get; set; }

        // Computed properties - ADD [NotMapped]
        [Display(Name = "Is Overdue")]
        [NotMapped]
        public bool IsOverdue => Status == MaintenanceStatus.Scheduled && ScheduledDate < DateTime.UtcNow;

        [Display(Name = "Days Until Due")]
        [NotMapped]
        public int DaysUntilDue => (int)(ScheduledDate - DateTime.UtcNow).TotalDays;

        [Display(Name = "Completion Rate")]
        [NotMapped]
        public string CompletionRate => PerformedDate.HasValue ? "100%" : "0%";
    }
}