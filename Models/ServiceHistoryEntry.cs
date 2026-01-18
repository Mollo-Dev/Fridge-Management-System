using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;

namespace GRP_03_27.Models
{
    public class ServiceHistoryEntry
    {
        [Key]
        public int ServiceHistoryEntryId { get; set; }

        [Required]
        public DateTime ServiceDate { get; set; } = DateTime.UtcNow;

        [Required]
        public ServiceType ServiceType { get; set; }

        [Required]
        public string Description { get; set; }

        public decimal Cost { get; set; }

        // Foreign keys
        public int FridgeId { get; set; }
        public string TechnicianId { get; set; }

        // Navigation properties
        public virtual Fridge Fridge { get; set; }
        public virtual User Technician { get; set; }
    }
}
