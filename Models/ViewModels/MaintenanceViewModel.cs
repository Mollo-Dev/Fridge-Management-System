using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GRP_03_27.Models.ViewModels
{
    public class MaintenanceDashboardViewModel
    {
        public int TodayMaintenanceCount { get; set; }
        public int WeeklyMaintenanceCount { get; set; }
        public int CompletedThisMonth { get; set; }
        public int HighPriorityMaintenance { get; set; }
        public List<MaintenanceRecord> UpcomingMaintenance { get; set; } = new List<MaintenanceRecord>();
        public List<MaintenanceRecord> RecentCompletions { get; set; } = new List<MaintenanceRecord>();
        public List<MaintenanceRecord> OverdueMaintenance { get; set; } = new List<MaintenanceRecord>();
    }

    public class ScheduleMaintenanceViewModel
    {
        [Required(ErrorMessage = "Fridge selection is required")]
        [Display(Name = "Select Fridge")]
        public int FridgeId { get; set; }

        [Required(ErrorMessage = "Technician assignment is required")]
        [Display(Name = "Assign Technician")]
        public string AssignedTechnicianId { get; set; }

        [Required(ErrorMessage = "Maintenance selection is required")]
        [Display(Name = "Select Maintenance")]
        public int MaintenanceRecordId { get; set; }

        [Required(ErrorMessage = "Scheduled date is required")]
        [Display(Name = "Scheduled Date")]
        [DataType(DataType.DateTime)]
        public DateTime ScheduledDate { get; set; } = DateTime.UtcNow.AddDays(1);

        [Required(ErrorMessage = "Maintenance type is required")]
        [Display(Name = "Maintenance Type")]
        public MaintenanceType MaintenanceType { get; set; }

        [Required(ErrorMessage = "Technician notes are required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Notes must be between 10 and 500 characters")]
        [Display(Name = "Technician Notes")]
        public string TechnicianNotes { get; set; }

        [Required(ErrorMessage = "Service checklist is required")]
        [StringLength(1000, MinimumLength = 20, ErrorMessage = "Checklist must be between 20 and 1000 characters")]
        [Display(Name = "Service Checklist")]
        public string ServiceChecklist { get; set; }

        // Dropdown options
        public List<SelectListItem> AvailableFridges { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> AvailableTechnicians { get; set; } = new List<SelectListItem>();
    }

    public class CompleteMaintenanceViewModel
    {
        public int MaintenanceRecordId { get; set; }
        public int FridgeId { get; set; }
        public string FridgeDetails { get; set; }
        public string CustomerName { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string TechnicianNotes { get; set; }
        public string ServiceChecklist { get; set; }
        public MaintenanceType MaintenanceType { get; set; }

        [Required(ErrorMessage = "Performed date is required")]
        [Display(Name = "Performed Date")]
        [DataType(DataType.DateTime)]
        public DateTime PerformedDate { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Updated notes are required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Updated notes must be between 10 and 500 characters")]
        [Display(Name = "Updated Technician Notes")]
        public string UpdatedTechnicianNotes { get; set; }

        [Required(ErrorMessage = "Completed checklist is required")]
        [StringLength(1000, MinimumLength = 20, ErrorMessage = "Completed checklist must be between 20 and 1000 characters")]
        [Display(Name = "Completed Checklist")]
        public string CompletedChecklist { get; set; }

        [Display(Name = "Additional Parts Used")]
        [StringLength(500, ErrorMessage = "Parts description cannot exceed 500 characters")]
        public string? PartsUsed { get; set; }

        [Display(Name = "Total Cost")]
        [Range(0, 10000, ErrorMessage = "Cost must be between 0 and 10,000")]
        public decimal TotalCost { get; set; }
    }

    public class MaintenanceIndexViewModel
    {
        public List<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
        public string StatusFilter { get; set; }
        public string TechnicianFilter { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public List<SelectListItem> StatusOptions { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> TechnicianOptions { get; set; } = new List<SelectListItem>();
    }
}