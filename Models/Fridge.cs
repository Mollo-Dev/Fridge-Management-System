using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations.Schema; // Add this using

namespace GRP_03_27.Models
{
    public class Fridge
    {
        [Key]
        public int FridgeId { get; set; }

        [Required(ErrorMessage = "Model is required")]
        [StringLength(100, ErrorMessage = "Model cannot exceed 100 characters")]
        public string Model { get; set; }

        [Required(ErrorMessage = "Serial number is required")]
        [StringLength(50, ErrorMessage = "Serial number cannot exceed 50 characters")]
        [Display(Name = "Serial Number")]
        public string SerialNumber { get; set; }

        [Required(ErrorMessage = "Purchase date is required")]
        [Display(Name = "Purchase Date")]
        [DataType(DataType.Date)]
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

        [Required]
        [Display(Name = "Status")]
        public FridgeStatus Status { get; set; } = FridgeStatus.Available;

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string Notes { get; set; }

        [Display(Name = "Last Service Date")]
        [DataType(DataType.Date)]
        public DateTime? LastServiceDate { get; set; }

        [Display(Name = "Next Service Date")]
        [DataType(DataType.Date)]
        public DateTime? NextServiceDate { get; set; }

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [Display(Name = "Allocation Date")]
        [DataType(DataType.Date)]
        public DateTime? AllocationDate { get; set; }

        [Display(Name = "Display Name")]
        [NotMapped] // ADD THIS
        public string DisplayName => $"{Model} - {SerialNumber}";

        // Foreign keys
        [Display(Name = "Customer")]
        public int? CustomerId { get; set; }

        [Required(ErrorMessage = "Supplier is required")]
        [Display(Name = "Supplier")]
        public int SupplierId { get; set; }

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Last Maintenance Date")]
        public DateTime? LastMaintenanceDate { get; set; }

        [Display(Name = "Next Maintenance Date")]
        public DateTime? NextMaintenanceDate { get; set; }

        [Display(Name = "Maintenance Count")]
        [NotMapped] // ADD THIS
        public int MaintenanceCount => MaintenanceRecords?.Count ?? 0;

        // Navigation properties
        public virtual Customer Customer { get; set; }
        public virtual Supplier Supplier { get; set; }
        public virtual ICollection<FaultReport> FaultReports { get; set; } = new List<FaultReport>();
        public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
        public virtual ICollection<ServiceHistoryEntry> ServiceHistory { get; set; } = new List<ServiceHistoryEntry>();
        public virtual ICollection<AllocationHistory> AllocationHistories { get; set; } = new List<AllocationHistory>();

        // Computed properties - ALL NEED [NotMapped]
        [Display(Name = "Age (Months)")]
        [NotMapped]
        public int AgeInMonths => (int)((DateTime.UtcNow - PurchaseDate).TotalDays / 30);

        [Display(Name = "Total Faults")]
        [NotMapped]
        public int TotalFaults => FaultReports?.Count ?? 0;

        [Display(Name = "Active Faults")]
        [NotMapped]
        public int ActiveFaults => FaultReports?.Count(fr =>
            fr.Status == FaultStatus.Reported ||
            fr.Status == FaultStatus.Diagnosed ||
            fr.Status == FaultStatus.Scheduled) ?? 0;
    }
}