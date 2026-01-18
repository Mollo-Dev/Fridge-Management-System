using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations;

namespace GRP_03_27.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }
        
        [Required]
        [StringLength(500)]
        public string Message { get; set; }
        
        [Required]
        public DateTime SentDate { get; set; }
        
        public bool IsRead { get; set; } = false;
        
        [Required]
        public NotificationType Type { get; set; }
        
        [Required]
        public NotificationPriority Priority { get; set; } = NotificationPriority.Medium;
        
        public int? CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }
        
        public string? TechnicianId { get; set; }
        public virtual User? Technician { get; set; }
        
        public string? AdminId { get; set; }
        public virtual User? Admin { get; set; }
        
        // Related entity references for context
        public int? RelatedEntityId { get; set; }
        public string? RelatedEntityType { get; set; } // "FaultReport", "MaintenanceRecord", "PurchaseRequest", etc.
        
        // Automatic notification settings
        public bool IsAutomatic { get; set; } = true;
        public string? TriggerEvent { get; set; } // "FaultReported", "MaintenanceScheduled", "PurchaseRequested", etc.
        
        // Expiry for time-sensitive notifications
        public DateTime? ExpiryDate { get; set; }
        
        // Additional data for complex notifications
        public string? AdditionalData { get; set; } // JSON string for extra information
    }
}
