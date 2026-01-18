using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using GRP_03_27.Models.ViewModels;
using GRP_03_27.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace GRP_03_27.Controllers
{
    [Authorize(Roles = "MaintenanceTechnician,Administrator")]
    public class MaintenanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<MaintenanceController> _logger;
        private readonly INotificationService _notificationService;

        public MaintenanceController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            ILogger<MaintenanceController> logger,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _notificationService = notificationService;
        }

        // GET: Maintenance/Index
        public async Task<IActionResult> Index(string statusFilter = "", string technicianFilter = "",
            DateTime? dateFrom = null, DateTime? dateTo = null, int page = 1, int pageSize = 10)
        {
            try
            {
                var maintenanceQuery = _context.MaintenanceRecords
                    .Include(m => m.Fridge)
                    .ThenInclude(f => f.Customer)
                    .Include(m => m.AssignedTechnician)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(statusFilter) && Enum.TryParse<MaintenanceStatus>(statusFilter, out var status))
                {
                    maintenanceQuery = maintenanceQuery.Where(m => m.Status == status);
                }

                if (!string.IsNullOrEmpty(technicianFilter))
                {
                    maintenanceQuery = maintenanceQuery.Where(m => m.AssignedTechnicianId == technicianFilter);
                }

                if (dateFrom.HasValue)
                {
                    maintenanceQuery = maintenanceQuery.Where(m => m.ScheduledDate >= dateFrom.Value);
                }

                if (dateTo.HasValue)
                {
                    maintenanceQuery = maintenanceQuery.Where(m => m.ScheduledDate <= dateTo.Value);
                }

                // Pagination
                var totalRecords = await maintenanceQuery.CountAsync();
                var maintenanceRecords = await maintenanceQuery
                    .OrderByDescending(m => m.ScheduledDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var technicians = await _userManager.GetUsersInRoleAsync("Technician");

                var viewModel = new MaintenanceIndexViewModel
                {
                    MaintenanceRecords = maintenanceRecords,
                    StatusFilter = statusFilter,
                    TechnicianFilter = technicianFilter,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    StatusOptions = Enum.GetValues(typeof(MaintenanceStatus))
                        .Cast<MaintenanceStatus>()
                        .Select(s => new SelectListItem
                        {
                            Value = s.ToString(),
                            Text = s.ToString()
                        })
                        .ToList(),
                    TechnicianOptions = technicians
                        .Where(t => t.IsActive)
                        .Select(t => new SelectListItem
                        {
                            Value = t.Id,
                            Text = $"{t.FirstName} {t.LastName}"
                        })
                        .ToList()
                };

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
                ViewBag.TotalRecords = totalRecords;

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance index");
                TempData["ErrorMessage"] = "An error occurred while loading maintenance records.";
                return View(new MaintenanceIndexViewModel());
            }
        }

        // GET: Maintenance/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var technicianId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(technicianId))
                {
                    _logger.LogWarning("Unauthorized access attempt to maintenance dashboard");
                    return RedirectToAction("AccessDenied", "Account");
                }

                var today = DateTime.UtcNow.Date;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                var endOfWeek = startOfWeek.AddDays(7);

                var dashboardData = new MaintenanceDashboardViewModel
                {
                    TodayMaintenanceCount = await _context.MaintenanceRecords
                        .CountAsync(m => m.ScheduledDate.Date == today &&
                                       m.Status == MaintenanceStatus.Scheduled),

                    WeeklyMaintenanceCount = await _context.MaintenanceRecords
                        .CountAsync(m => m.ScheduledDate >= startOfWeek &&
                                       m.ScheduledDate < endOfWeek &&
                                       m.Status == MaintenanceStatus.Scheduled),

                    CompletedThisMonth = await _context.MaintenanceRecords
                        .CountAsync(m => m.PerformedDate.HasValue &&
                                       m.PerformedDate.Value.Month == today.Month &&
                                       m.PerformedDate.Value.Year == today.Year &&
                                       m.Status == MaintenanceStatus.Completed),

                    HighPriorityMaintenance = await _context.MaintenanceRecords
                        .CountAsync(m => m.Status == MaintenanceStatus.Scheduled &&
                                       m.ScheduledDate <= today.AddDays(2)),

                    UpcomingMaintenance = await _context.MaintenanceRecords
                        .Include(m => m.Fridge)
                        .ThenInclude(f => f.Customer)
                        .Include(m => m.AssignedTechnician)
                        .Where(m => m.ScheduledDate >= today &&
                                  m.ScheduledDate <= today.AddDays(7) &&
                                  m.Status == MaintenanceStatus.Scheduled)
                        .OrderBy(m => m.ScheduledDate)
                        .Take(10)
                        .ToListAsync(),

                    RecentCompletions = await _context.MaintenanceRecords
                        .Include(m => m.Fridge)
                        .ThenInclude(f => f.Customer)
                        .Where(m => m.Status == MaintenanceStatus.Completed &&
                                  m.PerformedDate >= today.AddDays(-7))
                        .OrderByDescending(m => m.PerformedDate)
                        .Take(5)
                        .ToListAsync(),

                    OverdueMaintenance = await _context.MaintenanceRecords
                        .Include(m => m.Fridge)
                        .ThenInclude(f => f.Customer)
                        .Where(m => m.Status == MaintenanceStatus.Scheduled &&
                                  m.ScheduledDate < today)
                        .OrderBy(m => m.ScheduledDate)
                        .Take(5)
                        .ToListAsync()
                };

                _logger.LogInformation("Maintenance dashboard loaded successfully for technician {TechnicianId}", technicianId);
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance dashboard for user {UserId}", _userManager.GetUserId(User));
                TempData["ErrorMessage"] = "An error occurred while loading the maintenance dashboard.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Maintenance/Schedule
        public async Task<IActionResult> Schedule()
        {
            try
            {
                var viewModel = new ScheduleMaintenanceViewModel
                {
                    AvailableFridges = await GetAvailableFridgesForMaintenance(),
                    AvailableTechnicians = await GetAvailableTechnicians(),
                    ScheduledDate = DateTime.UtcNow.AddDays(1)
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance scheduling page");
                TempData["ErrorMessage"] = "An error occurred while loading the scheduling page.";
                return RedirectToAction("Dashboard");
            }
        }

        // POST: Maintenance/Schedule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Schedule(ScheduleMaintenanceViewModel viewModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed for maintenance scheduling");
                    viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                    viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                    TempData["ErrorMessage"] = "Please fix the validation errors below.";
                    return View(viewModel);
                }

                // Business rule validation: Check if technician is available
                var isTechnicianAvailable = await IsTechnicianAvailableAsync(
                    viewModel.AssignedTechnicianId,
                    viewModel.ScheduledDate);

                if (!isTechnicianAvailable)
                {
                    ModelState.AddModelError("AssignedTechnicianId",
                        "The selected technician is not available at the scheduled time.");
                    viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                    viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                    return View(viewModel);
                }

                // Business rule validation: Check if fridge is available for maintenance
                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .FirstOrDefaultAsync(f => f.FridgeId == viewModel.FridgeId);

                if (fridge == null)
                {
                    ModelState.AddModelError("FridgeId", "Selected fridge not found.");
                    viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                    viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                    return View(viewModel);
                }

                if (fridge.Status == FridgeStatus.Scrapped)
                {
                    ModelState.AddModelError("FridgeId", "Cannot schedule maintenance for a scrapped fridge.");
                    viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                    viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                    return View(viewModel);
                }

                var maintenanceRecord = new MaintenanceRecord
                {
                    FridgeId = viewModel.FridgeId,
                    AssignedTechnicianId = viewModel.AssignedTechnicianId,
                    ScheduledDate = viewModel.ScheduledDate,
                    TechnicianNotes = SanitizeHtmlInput(viewModel.TechnicianNotes),
                    ServiceChecklist = SanitizeHtmlInput(viewModel.ServiceChecklist),
                    MaintenanceType = viewModel.MaintenanceType,
                    Status = MaintenanceStatus.Scheduled,
                    CreatedDate = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow
                };

                _context.MaintenanceRecords.Add(maintenanceRecord);

                // Update fridge status if needed
                if (fridge.Status != FridgeStatus.UnderMaintenance)
                {
                    fridge.Status = FridgeStatus.UnderMaintenance;
                    fridge.LastUpdated = DateTime.UtcNow;
                    _context.Fridges.Update(fridge);
                }

                await _context.SaveChangesAsync();

                // Create automatic notification
                await _notificationService.CreateMaintenanceNotificationAsync(maintenanceRecord);

                // Log the activity
                _logger.LogInformation(
                    "Maintenance scheduled: ID {MaintenanceRecordId} for fridge {FridgeId} by technician {TechnicianId}",
                    maintenanceRecord.MaintenanceRecordId, viewModel.FridgeId, viewModel.AssignedTechnicianId);

                TempData["SuccessMessage"] = "Maintenance scheduled successfully!";
                return RedirectToAction("Details", new { id = maintenanceRecord.MaintenanceRecordId });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error scheduling maintenance");
                TempData["ErrorMessage"] = "A database error occurred while scheduling maintenance.";
                viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error scheduling maintenance");
                TempData["ErrorMessage"] = "An unexpected error occurred while scheduling maintenance.";
                viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                return View(viewModel);
            }
        }

        // GET: Maintenance/Complete/5
        public async Task<IActionResult> Complete(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    _logger.LogWarning("Invalid maintenance record ID requested: {MaintenanceRecordId}", id);
                    return BadRequest("Invalid maintenance record ID.");
                }

                var maintenanceRecord = await _context.MaintenanceRecords
                    .Include(m => m.Fridge)
                    .ThenInclude(f => f.Customer)
                    .Include(m => m.AssignedTechnician)
                    .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);

                if (maintenanceRecord == null)
                {
                    _logger.LogWarning("Maintenance record not found: ID {MaintenanceRecordId}", id);
                    return NotFound();
                }

                // Authorization check - only assigned technician or admin can complete
                var currentUserId = _userManager.GetUserId(User);
                var isAdmin = User.IsInRole("Admin");

                if (maintenanceRecord.AssignedTechnicianId != currentUserId && !isAdmin)
                {
                    _logger.LogWarning(
                        "Unauthorized completion attempt: User {UserId} tried to complete maintenance {MaintenanceRecordId}",
                        currentUserId, id);
                    return RedirectToAction("AccessDenied", "Account");
                }

                if (maintenanceRecord.Status == MaintenanceStatus.Completed)
                {
                    TempData["WarningMessage"] = "This maintenance record is already completed.";
                    return RedirectToAction("Details", new { id });
                }

                var viewModel = new CompleteMaintenanceViewModel
                {
                    MaintenanceRecordId = maintenanceRecord.MaintenanceRecordId,
                    FridgeId = maintenanceRecord.FridgeId,
                    FridgeDetails = $"{maintenanceRecord.Fridge.Model} (SN: {maintenanceRecord.Fridge.SerialNumber})",
                    CustomerName = maintenanceRecord.Fridge.Customer?.BusinessName ?? "Unknown",
                    ScheduledDate = maintenanceRecord.ScheduledDate,
                    TechnicianNotes = maintenanceRecord.TechnicianNotes,
                    ServiceChecklist = maintenanceRecord.ServiceChecklist,
                    MaintenanceType = maintenanceRecord.MaintenanceType,
                    PerformedDate = DateTime.UtcNow
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance completion page for ID {MaintenanceRecordId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the completion page.";
                return RedirectToAction("Index");
            }
        }

        // POST: Maintenance/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(int id, CompleteMaintenanceViewModel viewModel)
        {
            try
            {
                if (id != viewModel.MaintenanceRecordId)
                {
                    _logger.LogWarning("ID mismatch in maintenance completion: Route {RouteId} vs Model {ModelId}",
                        id, viewModel.MaintenanceRecordId);
                    return BadRequest("ID mismatch.");
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Model validation failed for maintenance completion");
                    return View(viewModel);
                }

                var maintenanceRecord = await _context.MaintenanceRecords
                    .Include(m => m.Fridge)
                    .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);

                if (maintenanceRecord == null)
                {
                    _logger.LogWarning("Maintenance record not found for completion: ID {MaintenanceRecordId}", id);
                    return NotFound();
                }

                // Authorization check
                var currentUserId = _userManager.GetUserId(User);
                var isAdmin = User.IsInRole("Admin");

                if (maintenanceRecord.AssignedTechnicianId != currentUserId && !isAdmin)
                {
                    _logger.LogWarning(
                        "Unauthorized completion: User {UserId} tried to complete maintenance {MaintenanceRecordId}",
                        currentUserId, id);
                    return RedirectToAction("AccessDenied", "Account");
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Update maintenance record
                    maintenanceRecord.PerformedDate = viewModel.PerformedDate;
                    maintenanceRecord.TechnicianNotes = SanitizeHtmlInput(viewModel.UpdatedTechnicianNotes);
                    maintenanceRecord.ServiceChecklist = SanitizeHtmlInput(viewModel.CompletedChecklist);
                    maintenanceRecord.Status = MaintenanceStatus.Completed;
                    maintenanceRecord.LastUpdated = DateTime.UtcNow;
                    maintenanceRecord.PartsUsed = viewModel.PartsUsed;
                    maintenanceRecord.TotalCost = viewModel.TotalCost;

                    _context.MaintenanceRecords.Update(maintenanceRecord);

                    // Update fridge status back to allocated and set maintenance dates
                    if (maintenanceRecord.Fridge != null)
                    {
                        if (maintenanceRecord.Fridge.Status == FridgeStatus.UnderMaintenance)
                        {
                            maintenanceRecord.Fridge.Status = FridgeStatus.Allocated;
                        }
                        maintenanceRecord.Fridge.LastMaintenanceDate = viewModel.PerformedDate;
                        maintenanceRecord.Fridge.NextMaintenanceDate = viewModel.PerformedDate.AddMonths(6); // 6 months until next maintenance
                        maintenanceRecord.Fridge.LastUpdated = DateTime.UtcNow;
                        _context.Fridges.Update(maintenanceRecord.Fridge);
                    }

                    // Create service history entry
                    var serviceHistory = new ServiceHistoryEntry
                    {
                        FridgeId = maintenanceRecord.FridgeId,
                        TechnicianId = currentUserId,
                        ServiceDate = viewModel.PerformedDate,
                        ServiceType = ServiceType.Maintenance,
                        Description = $"{maintenanceRecord.MaintenanceType} maintenance completed",
                        Cost = viewModel.TotalCost
                    };

                    _context.ServiceHistoryEntries.Add(serviceHistory);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation(
                        "Maintenance completed successfully: ID {MaintenanceRecordId} by technician {TechnicianId}",
                        id, currentUserId);

                    TempData["SuccessMessage"] = "Maintenance completed successfully!";
                    return RedirectToAction("Details", new { id });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during maintenance completion");
                    throw;
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error completing maintenance {MaintenanceRecordId}", id);
                TempData["ErrorMessage"] = "A database error occurred while completing maintenance.";
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error completing maintenance {MaintenanceRecordId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while completing maintenance.";
                return View(viewModel);
            }
        }

        // GET: Maintenance/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    return BadRequest("Invalid maintenance record ID.");
                }

                var maintenanceRecord = await _context.MaintenanceRecords
                    .Include(m => m.Fridge)
                    .ThenInclude(f => f.Customer)
                    .Include(m => m.AssignedTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);

                if (maintenanceRecord == null)
                {
                    return NotFound();
                }

                return View(maintenanceRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance details for ID {MaintenanceRecordId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading maintenance details.";
                return RedirectToAction("Index");
            }
        }

        // GET: Maintenance/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    return BadRequest("Invalid maintenance record ID.");
                }

                var maintenanceRecord = await _context.MaintenanceRecords
                    .Include(m => m.Fridge)
                    .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);

                if (maintenanceRecord == null)
                {
                    return NotFound();
                }

                var viewModel = new ScheduleMaintenanceViewModel
                {
                    MaintenanceRecordId = maintenanceRecord.MaintenanceRecordId,
                    FridgeId = maintenanceRecord.FridgeId,
                    AssignedTechnicianId = maintenanceRecord.AssignedTechnicianId,
                    ScheduledDate = maintenanceRecord.ScheduledDate,
                    MaintenanceType = maintenanceRecord.MaintenanceType,
                    TechnicianNotes = maintenanceRecord.TechnicianNotes,
                    ServiceChecklist = maintenanceRecord.ServiceChecklist,
                    AvailableFridges = await GetAvailableFridgesForMaintenance(),
                    AvailableTechnicians = await GetAvailableTechnicians()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading maintenance edit page for ID {MaintenanceRecordId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the edit page.";
                return RedirectToAction("Index");
            }
        }

        // POST: Maintenance/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ScheduleMaintenanceViewModel viewModel)
        {
            try
            {
                if (id != viewModel.MaintenanceRecordId)
                {
                    return BadRequest("ID mismatch.");
                }

                if (!ModelState.IsValid)
                {
                    viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                    viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                    return View(viewModel);
                }

                var maintenanceRecord = await _context.MaintenanceRecords.FindAsync(id);
                if (maintenanceRecord == null)
                {
                    return NotFound();
                }

                maintenanceRecord.FridgeId = viewModel.FridgeId;
                maintenanceRecord.AssignedTechnicianId = viewModel.AssignedTechnicianId;
                maintenanceRecord.ScheduledDate = viewModel.ScheduledDate;
                maintenanceRecord.MaintenanceType = viewModel.MaintenanceType;
                maintenanceRecord.TechnicianNotes = SanitizeHtmlInput(viewModel.TechnicianNotes);
                maintenanceRecord.ServiceChecklist = SanitizeHtmlInput(viewModel.ServiceChecklist);
                maintenanceRecord.LastUpdated = DateTime.UtcNow;

                _context.MaintenanceRecords.Update(maintenanceRecord);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Maintenance record updated successfully!";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating maintenance record {MaintenanceRecordId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the maintenance record.";
                viewModel.AvailableFridges = await GetAvailableFridgesForMaintenance();
                viewModel.AvailableTechnicians = await GetAvailableTechnicians();
                return View(viewModel);
            }
        }

        // POST: Maintenance/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string cancellationReason)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest("Invalid maintenance record ID.");
                }

                if (string.IsNullOrWhiteSpace(cancellationReason) || cancellationReason.Length < 5)
                {
                    TempData["ErrorMessage"] = "Please provide a valid cancellation reason (minimum 5 characters).";
                    return RedirectToAction("Details", new { id });
                }

                var maintenanceRecord = await _context.MaintenanceRecords
                    .Include(m => m.Fridge)
                    .FirstOrDefaultAsync(m => m.MaintenanceRecordId == id);

                if (maintenanceRecord == null)
                {
                    return NotFound();
                }

                maintenanceRecord.Status = MaintenanceStatus.Cancelled;
                maintenanceRecord.TechnicianNotes += $"\nCancelled on {DateTime.UtcNow}: {cancellationReason}";
                maintenanceRecord.LastUpdated = DateTime.UtcNow;

                // Update fridge status back to allocated if it was under maintenance
                if (maintenanceRecord.Fridge != null && maintenanceRecord.Fridge.Status == FridgeStatus.UnderMaintenance)
                {
                    maintenanceRecord.Fridge.Status = FridgeStatus.Allocated;
                    maintenanceRecord.Fridge.LastUpdated = DateTime.UtcNow;
                    _context.Fridges.Update(maintenanceRecord.Fridge);
                }

                _context.MaintenanceRecords.Update(maintenanceRecord);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Maintenance record cancelled successfully.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling maintenance record {MaintenanceRecordId}", id);
                TempData["ErrorMessage"] = "An error occurred while cancelling the maintenance record.";
                return RedirectToAction("Details", new { id });
            }
        }

        #region Helper Methods

        private async Task<List<SelectListItem>> GetAvailableFridgesForMaintenance()
        {
            var fridges = await _context.Fridges
                .Include(f => f.Customer)
                .Where(f => f.Status != FridgeStatus.Scrapped && f.IsActive)
                .OrderBy(f => f.Customer.BusinessName)
                .ThenBy(f => f.Model)
                .Select(f => new SelectListItem
                {
                    Value = f.FridgeId.ToString(),
                    Text = $"{f.Customer.BusinessName} - {f.Model} (SN: {f.SerialNumber})"
                })
                .ToListAsync();

            return fridges;
        }

        private async Task<List<SelectListItem>> GetAvailableTechnicians()
        {
            var technicians = await _userManager.GetUsersInRoleAsync("MaintenanceTechnician");
            return technicians
                .Where(t => t.IsActive)
                .OrderBy(t => t.FirstName)
                .ThenBy(t => t.LastName)
                .Select(t => new SelectListItem
                {
                    Value = t.Id,
                    Text = $"{t.FirstName} {t.LastName} ({t.Email})"
                })
                .ToList();
        }

        private async Task<bool> IsTechnicianAvailableAsync(string technicianId, DateTime scheduledDate)
        {
            // Check if technician has other maintenance scheduled at the same time (±2 hours)
            var startTime = scheduledDate.AddHours(-2);
            var endTime = scheduledDate.AddHours(2);

            var conflictingMaintenance = await _context.MaintenanceRecords
                .AnyAsync(m => m.AssignedTechnicianId == technicianId &&
                             m.ScheduledDate >= startTime &&
                             m.ScheduledDate <= endTime &&
                             m.Status == MaintenanceStatus.Scheduled);

            return !conflictingMaintenance;
        }

        private string SanitizeHtmlInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove potentially dangerous HTML tags
            var sanitized = Regex.Replace(input, @"<script[^>]*>.*?</script>", "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            sanitized = Regex.Replace(sanitized, @"<[^>]*(>|$)", string.Empty);

            // Limit length
            return sanitized.Length > 1000 ? sanitized.Substring(0, 1000) : sanitized;
        }

        #endregion
    }
}