using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace GRP_03_27.Models
{
    public class User : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [Required]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Employee ID")]
        [StringLength(20)]
        public string? EmployeeId { get; set; }

        [Display(Name = "Specialization")]
        [StringLength(100)]
        public string? Specialization { get; set; }

        // Navigation properties for maintenance
        public virtual ICollection<MaintenanceRecord> AssignedMaintenance { get; set; } = new List<MaintenanceRecord>();
        public virtual ICollection<ServiceHistoryEntry> ServiceHistory { get; set; } = new List<ServiceHistoryEntry>();

        // Navigation properties
        public virtual ICollection<FaultReport> FaultReports { get; set; }
        public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; }
        public virtual ICollection<FaultReport> AssignedFaultReports { get; set; } = new List<FaultReport>();

        // Computed property for full name
        public string FullName => $"{FirstName} {LastName}";
    }
}
