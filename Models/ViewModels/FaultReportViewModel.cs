using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;

namespace GRP_03_27.Models.ViewModels
{
    public class CreateFaultReportViewModel
    {
        [Required(ErrorMessage = "Please select a fridge")]
        [Display(Name = "Select Fridge")]
        public int FridgeId { get; set; }

        [Required(ErrorMessage = "Fault description is required")]
        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
        [Display(Name = "Fault Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Request Replacement Fridge")]
        public bool RequestReplacement { get; set; }

        [Display(Name = "Additional Notes (Optional)")]
        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters")]
        public string? CustomerNotes { get; set; }

        // For displaying available fridges
        public List<Fridge>? AvailableFridges { get; set; }
    }

    public class UpdateFaultReportViewModel
    {
        public int FaultReportId { get; set; }

        [Required(ErrorMessage = "Status is required")]
        [Display(Name = "Status")]
        public FaultStatus Status { get; set; }

        [StringLength(2000, ErrorMessage = "Diagnosis details cannot exceed 2000 characters")]
        [Display(Name = "Diagnosis Details")]
        public string? DiagnosisDetails { get; set; }

        [Display(Name = "Scheduled Repair Date")]
        [DataType(DataType.Date)]
        public DateTime? ScheduledDate { get; set; }

        [Display(Name = "Assigned Technician")]
        public string? AssignedTechnicianId { get; set; }

        [Display(Name = "Parts Required")]
        [StringLength(500, ErrorMessage = "Parts description cannot exceed 500 characters")]
        public string? PartsRequired { get; set; }

        [Display(Name = "Estimated Repair Cost")]
        [Range(0, 100000, ErrorMessage = "Repair cost must be reasonable")]
        public decimal? RepairCost { get; set; }

        [Display(Name = "Internal Notes")]
        [StringLength(1000, ErrorMessage = "Internal notes cannot exceed 1000 characters")]
        public string? InternalNotes { get; set; }

        [Display(Name = "Approve Replacement Request")]
        public bool ReplacementApproved { get; set; }

        // For displaying technicians
        public List<User>? AvailableTechnicians { get; set; }
    }

    public class FaultReportDashboardViewModel
    {
        public int TotalReports { get; set; }
        public int PendingReports { get; set; }
        public int InProgressReports { get; set; }
        public int ResolvedReports { get; set; }
        public int OverdueReports { get; set; }
        public List<FaultReport> RecentReports { get; set; } = new List<FaultReport>();
        public List<FaultReport> HighPriorityReports { get; set; } = new List<FaultReport>();
    }
}