using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Services
{
    public interface INotificationService
    {
        Task CreateNotificationAsync(string message, NotificationType type, NotificationPriority priority, 
            int? customerId = null, string? technicianId = null, string? adminId = null,
            int? relatedEntityId = null, string? relatedEntityType = null, string? triggerEvent = null,
            DateTime? expiryDate = null, string? additionalData = null);
        
        Task CreateFaultReportNotificationAsync(FaultReport faultReport);
        Task CreateMaintenanceNotificationAsync(MaintenanceRecord maintenanceRecord);
        Task CreatePurchaseRequestNotificationAsync(PurchaseRequest purchaseRequest);
        Task CreateOverdueNotificationAsync();
        Task CreateLowStockNotificationAsync();
        
        Task<List<Notification>> GetNotificationsForUserAsync(string userId, int limit = 10);
        Task<List<Notification>> GetNotificationsForCustomerAsync(int customerId, int limit = 10);
        Task MarkAsReadAsync(int notificationId);
        Task MarkAllAsReadAsync(string userId);
        Task<int> GetUnreadCountAsync(string userId);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ApplicationDbContext context, UserManager<User> userManager, ILogger<NotificationService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task CreateNotificationAsync(string message, NotificationType type, NotificationPriority priority,
            int? customerId = null, string? technicianId = null, string? adminId = null,
            int? relatedEntityId = null, string? relatedEntityType = null, string? triggerEvent = null,
            DateTime? expiryDate = null, string? additionalData = null)
        {
            try
            {
                var notification = new Notification
                {
                    Message = message,
                    Type = type,
                    Priority = priority,
                    SentDate = DateTime.UtcNow,
                    IsRead = false,
                    CustomerId = customerId,
                    TechnicianId = technicianId,
                    AdminId = adminId,
                    RelatedEntityId = relatedEntityId,
                    RelatedEntityType = relatedEntityType,
                    TriggerEvent = triggerEvent,
                    ExpiryDate = expiryDate,
                    AdditionalData = additionalData,
                    IsAutomatic = true
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Notification created: {Message} for {RecipientType}", 
                    message, customerId != null ? "Customer" : technicianId != null ? "Technician" : "Admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notification: {Message}", message);
            }
        }

        public async Task CreateFaultReportNotificationAsync(FaultReport faultReport)
        {
            var message = $"New fault report #{faultReport.FaultReportId}: {faultReport.Description}";
            var priority = faultReport.Priority switch
            {
                PriorityLevel.High => NotificationPriority.High,
                PriorityLevel.Medium => NotificationPriority.Medium,
                PriorityLevel.Low => NotificationPriority.Low,
                _ => NotificationPriority.Medium
            };

            // Notify technicians and admins
            var technicians = await _userManager.GetUsersInRoleAsync("FaultTechnician");
            var admins = await _userManager.GetUsersInRoleAsync("Administrator");
            var allTechnicians = technicians.Concat(admins).Distinct().ToList();

            foreach (var technician in allTechnicians)
            {
                await CreateNotificationAsync(
                    message,
                    NotificationType.Fault,
                    priority,
                    technicianId: technician.Id,
                    relatedEntityId: faultReport.FaultReportId,
                    relatedEntityType: "FaultReport",
                    triggerEvent: "FaultReported"
                );
            }

            // Notify customer
            if (faultReport.CustomerId > 0)
            {
                await CreateNotificationAsync(
                    $"Your fault report #{faultReport.FaultReportId} has been received and is being processed.",
                    NotificationType.Info,
                    NotificationPriority.Medium,
                    customerId: faultReport.CustomerId,
                    relatedEntityId: faultReport.FaultReportId,
                    relatedEntityType: "FaultReport",
                    triggerEvent: "FaultReported"
                );
            }
        }

        public async Task CreateMaintenanceNotificationAsync(MaintenanceRecord maintenanceRecord)
        {
            var message = $"Maintenance scheduled for fridge #{maintenanceRecord.FridgeId} on {maintenanceRecord.ScheduledDate:MMM dd, yyyy}";
            
            // Notify assigned technician
            if (!string.IsNullOrEmpty(maintenanceRecord.AssignedTechnicianId))
            {
                await CreateNotificationAsync(
                    message,
                    NotificationType.Maintenance,
                    NotificationPriority.Medium,
                    technicianId: maintenanceRecord.AssignedTechnicianId,
                    relatedEntityId: maintenanceRecord.MaintenanceRecordId,
                    relatedEntityType: "MaintenanceRecord",
                    triggerEvent: "MaintenanceScheduled"
                );
            }

            // Notify customer
            var fridge = await _context.Fridges
                .Include(f => f.Customer)
                .FirstOrDefaultAsync(f => f.FridgeId == maintenanceRecord.FridgeId);

            if (fridge?.CustomerId > 0)
            {
                await CreateNotificationAsync(
                    $"Maintenance scheduled for your fridge on {maintenanceRecord.ScheduledDate:MMM dd, yyyy}",
                    NotificationType.Maintenance,
                    NotificationPriority.Medium,
                    customerId: fridge.CustomerId,
                    relatedEntityId: maintenanceRecord.MaintenanceRecordId,
                    relatedEntityType: "MaintenanceRecord",
                    triggerEvent: "MaintenanceScheduled"
                );
            }
        }

        public async Task CreatePurchaseRequestNotificationAsync(PurchaseRequest purchaseRequest)
        {
            var message = $"New purchase request #{purchaseRequest.PurchaseRequestId}: {purchaseRequest.Quantity} fridges requested";
            
            // Notify purchasing managers and admins
            var purchasingManagers = await _userManager.GetUsersInRoleAsync("PurchasingManager");
            var admins = await _userManager.GetUsersInRoleAsync("Administrator");
            var allAdmins = purchasingManagers.Concat(admins).Distinct().ToList();

            foreach (var admin in allAdmins)
            {
                await CreateNotificationAsync(
                    message,
                    NotificationType.Purchase,
                    NotificationPriority.Medium,
                    adminId: admin.Id,
                    relatedEntityId: purchaseRequest.PurchaseRequestId,
                    relatedEntityType: "PurchaseRequest",
                    triggerEvent: "PurchaseRequested"
                );
            }
        }

        public async Task CreateOverdueNotificationAsync()
        {
            var today = DateTime.UtcNow.Date;
            
            // Overdue fault reports
            var overdueFaults = await _context.FaultReports
                .Where(fr => (fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed) &&
                            fr.DateReported < today.AddDays(-7))
                .ToListAsync();

            foreach (var fault in overdueFaults)
            {
                var message = $"Fault report #{fault.FaultReportId} is overdue and needs attention";
                
                // Notify technicians
                var technicians = await _userManager.GetUsersInRoleAsync("FaultTechnician");
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                var allTechnicians = technicians.Concat(admins).Distinct().ToList();

                foreach (var technician in allTechnicians)
                {
                    await CreateNotificationAsync(
                        message,
                        NotificationType.Warning,
                        NotificationPriority.High,
                        technicianId: technician.Id,
                        relatedEntityId: fault.FaultReportId,
                        relatedEntityType: "FaultReport",
                        triggerEvent: "OverdueFault"
                    );
                }
            }

            // Overdue maintenance
            var overdueMaintenance = await _context.MaintenanceRecords
                .Where(m => m.Status == MaintenanceStatus.Scheduled && m.ScheduledDate < today)
                .ToListAsync();

            foreach (var maintenance in overdueMaintenance)
            {
                var message = $"Maintenance #{maintenance.MaintenanceRecordId} is overdue";
                
                if (!string.IsNullOrEmpty(maintenance.AssignedTechnicianId))
                {
                    await CreateNotificationAsync(
                        message,
                        NotificationType.Warning,
                        NotificationPriority.High,
                        technicianId: maintenance.AssignedTechnicianId,
                        relatedEntityId: maintenance.MaintenanceRecordId,
                        relatedEntityType: "MaintenanceRecord",
                        triggerEvent: "OverdueMaintenance"
                    );
                }
            }
        }

        public async Task CreateLowStockNotificationAsync()
        {
            var availableFridges = await _context.Fridges
                .CountAsync(f => f.Status == FridgeStatus.Available);

            if (availableFridges <= 5) // Low stock threshold
            {
                var message = $"Low stock alert: Only {availableFridges} fridges available";
                
                // Notify inventory managers and admins
                var inventoryManagers = await _userManager.GetUsersInRoleAsync("InventoryLiaison");
                var admins = await _userManager.GetUsersInRoleAsync("Administrator");
                var allAdmins = inventoryManagers.Concat(admins).Distinct().ToList();

                foreach (var admin in allAdmins)
                {
                    await CreateNotificationAsync(
                        message,
                        NotificationType.Warning,
                        NotificationPriority.High,
                        adminId: admin.Id,
                        triggerEvent: "LowStock"
                    );
                }
            }
        }

        public async Task<List<Notification>> GetNotificationsForUserAsync(string userId, int limit = 10)
        {
            return await _context.Notifications
                .Where(n => n.TechnicianId == userId || n.AdminId == userId)
                .OrderByDescending(n => n.SentDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<List<Notification>> GetNotificationsForCustomerAsync(int customerId, int limit = 10)
        {
            return await _context.Notifications
                .Where(n => n.CustomerId == customerId)
                .OrderByDescending(n => n.SentDate)
                .Take(limit)
                .ToListAsync();
        }

        public async Task MarkAsReadAsync(int notificationId)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var notifications = await _context.Notifications
                .Where(n => (n.TechnicianId == userId || n.AdminId == userId) && !n.IsRead)
                .ToListAsync();

            foreach (var notification in notifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications
                .CountAsync(n => (n.TechnicianId == userId || n.AdminId == userId) && !n.IsRead);
        }
    }
}
