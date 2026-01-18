using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using GRP_03_27.Models.ViewModels; // Add this line
using GRP_03_27.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using static GRP_03_27.Models.ViewModels.UpdateFaultReportViewModel;

namespace GRP_03_27.Controllers
{
    [Authorize]
    public class FaultReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<FaultReportsController> _logger;
        private readonly INotificationService _notificationService;

        public FaultReportsController(ApplicationDbContext context, UserManager<User> userManager, 
            ILogger<FaultReportsController> logger, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _notificationService = notificationService;
        }


        // GET: FaultReports - Role-based view
        public async Task<IActionResult> Index(string statusFilter = "All", string priorityFilter = "All", int page = 1, int pageSize = 10)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var userRoles = await _userManager.GetRolesAsync(user);

                IQueryable<FaultReport> faultReports;

                if (userRoles.Contains("FaultTechnician") || userRoles.Contains("Administrator"))
                {
                    // Technicians see all fault reports with comprehensive filtering
                    faultReports = _context.FaultReports
                        .Include(fr => fr.Customer)
                        .Include(fr => fr.Fridge)
                        .Include(fr => fr.AssignedTechnician)
                        .AsQueryable();

                    // Status filter
                    if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
                    {
                        if (Enum.TryParse<FaultStatus>(statusFilter, out var status))
                        {
                            faultReports = faultReports.Where(fr => fr.Status == status);
                        }
                    }

                    // Priority filter
                    if (!string.IsNullOrEmpty(priorityFilter) && priorityFilter != "All")
                    {
                        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
                        var fourteenDaysAgo = DateTime.UtcNow.AddDays(-14);

                        faultReports = priorityFilter switch
                        {
                            // High: Replacement requested OR older than 14 days
                            "High" => faultReports.Where(fr => fr.RequestReplacement || fr.DateReported <= fourteenDaysAgo),
                            // Medium: Not replacement, between 8 and 14 days inclusive
                            "Medium" => faultReports.Where(fr => !fr.RequestReplacement && fr.DateReported < sevenDaysAgo && fr.DateReported > fourteenDaysAgo),
                            // Low: Not replacement, reported within last 7 days
                            "Low" => faultReports.Where(fr => !fr.RequestReplacement && fr.DateReported >= sevenDaysAgo),
                            _ => faultReports
                        };
                    }

                    // Add statistics for technicians
                    ViewBag.PendingCount = await _context.FaultReports.CountAsync(fr =>
                        fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed);
                    ViewBag.OverdueCount = await _context.FaultReports.CountAsync(fr =>
                        (fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed) &&
                        fr.DateReported <= DateTime.UtcNow.AddDays(-7));
                    ViewBag.ResolvedCount = await _context.FaultReports.CountAsync(fr =>
                        fr.Status == FaultStatus.Resolved || fr.Status == FaultStatus.Closed);
                    ViewBag.MyAssignedCount = await _context.FaultReports.CountAsync(fr =>
                        fr.AssignedTechnicianId == user.Id &&
                        fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed);
                    ViewBag.UnassignedCount = await _context.FaultReports.CountAsync(fr =>
                        string.IsNullOrEmpty(fr.AssignedTechnicianId) &&
                        fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed);
                }
                else
                {
                    // Customers see only their fault reports
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.UserId == user.Id);

                    if (customer == null)
                    {
                        TempData["ErrorMessage"] = "Customer profile not found.";
                        return View(new List<FaultReport>());
                    }

                    faultReports = _context.FaultReports
                        .Include(fr => fr.Customer)
                        .Include(fr => fr.Fridge)
                        .Include(fr => fr.AssignedTechnician)
                        .Where(fr => fr.CustomerId == customer.CustomerId)
                        .AsQueryable();

                    // Add customer-specific statistics
                    ViewBag.PendingCount = await _context.FaultReports.CountAsync(fr =>
                        fr.CustomerId == customer.CustomerId &&
                        (fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed));
                    ViewBag.ResolvedCount = await _context.FaultReports.CountAsync(fr =>
                        fr.CustomerId == customer.CustomerId &&
                        (fr.Status == FaultStatus.Resolved || fr.Status == FaultStatus.Closed));
                    ViewBag.ActiveFaultsCount = await _context.Fridges.CountAsync(f =>
                        f.CustomerId == customer.CustomerId &&
                        f.FaultReports.Any(fr => fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed));
                }

                // Pagination
                var totalCount = await faultReports.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var faultReportList = await faultReports
                    .OrderByDescending(fr => fr.DateReported)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.StatusFilter = statusFilter;
                ViewBag.PriorityFilter = priorityFilter;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;

                ViewBag.StatusOptions = new SelectList(Enum.GetValues(typeof(FaultStatus)).Cast<FaultStatus>(), statusFilter);
                ViewBag.PriorityOptions = new SelectList(new[] { "All", "High", "Medium", "Low" }, priorityFilter);

                return View(faultReportList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fault reports");
                TempData["ErrorMessage"] = "An error occurred while retrieving fault reports.";
                return View(new List<FaultReport>());
            }
        }

        // GET: FaultReports/GetReportsCount - For real-time updates
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> GetReportsCount()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                var counts = new
                {
                    TotalCount = await _context.FaultReports.CountAsync(),
                    PendingCount = await _context.FaultReports.CountAsync(fr =>
                        fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed),
                    OverdueCount = await _context.FaultReports.CountAsync(fr =>
                        (fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed) &&
                        (DateTime.UtcNow - fr.DateReported).TotalDays > 7),
                    ResolvedCount = await _context.FaultReports.CountAsync(fr =>
                        fr.Status == FaultStatus.Resolved || fr.Status == FaultStatus.Closed),
                    MyAssignedCount = await _context.FaultReports.CountAsync(fr =>
                        fr.AssignedTechnicianId == user.Id &&
                        fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed),
                    UnassignedCount = await _context.FaultReports.CountAsync(fr =>
                        string.IsNullOrEmpty(fr.AssignedTechnicianId) &&
                        fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed),
                    LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                };

                return Json(counts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting report counts");
                return Json(new { error = "Unable to get counts" });
            }
        }

        // GET: FaultReports/Dashboard - Technician Dashboard
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var dashboardData = new FaultReportDashboardViewModel
                {
                    TotalReports = await _context.FaultReports.CountAsync(),
                    PendingReports = await _context.FaultReports.CountAsync(fr =>
                        fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed),
                    InProgressReports = await _context.FaultReports.CountAsync(fr =>
                        fr.Status == FaultStatus.Scheduled || fr.Status == FaultStatus.InProgress),
                    ResolvedReports = await _context.FaultReports.CountAsync(fr =>
                        fr.Status == FaultStatus.Resolved || fr.Status == FaultStatus.Closed),
                    OverdueReports = await _context.FaultReports.CountAsync(fr =>
                        (fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed) &&
                        fr.DateReported <= DateTime.UtcNow.AddDays(-7)),
                    RecentReports = await _context.FaultReports
                        .Include(fr => fr.Customer)
                        .Include(fr => fr.Fridge)
                        .OrderByDescending(fr => fr.DateReported)
                        .Take(5)
                        .ToListAsync(),
                    HighPriorityReports = await _context.FaultReports
                        .Include(fr => fr.Customer)
                        .Include(fr => fr.Fridge)
                        .Where(fr => fr.RequestReplacement || fr.DateReported <= DateTime.UtcNow.AddDays(-14))
                        .Where(fr => fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed)
                        .OrderByDescending(fr => fr.DateReported)
                        .Take(5)
                        .ToListAsync()
                };

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fault report dashboard");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return View(new FaultReportDashboardViewModel());
            }
        }

        // GET: FaultReports/Create
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == user.Id);

                if (customer == null)
                {
                    TempData["ErrorMessage"] = "Customer profile not found. Please complete your profile first.";
                    return RedirectToAction("CreateProfile", "Customer");
                }

                var availableFridges = await _context.Fridges
                    .Where(f => f.CustomerId == customer.CustomerId && f.Status != FridgeStatus.Scrapped)
                    .ToListAsync();

                if (!availableFridges.Any())
                {
                    TempData["WarningMessage"] = "You don't have any fridges allocated to your account. Please contact customer support.";
                    return RedirectToAction("MyFridges", "Customer");
                }

                var viewModel = new CreateFaultReportViewModel
                {
                    AvailableFridges = availableFridges
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fault report creation form");
                TempData["ErrorMessage"] = "An error occurred while loading the fault report form.";
                return RedirectToAction("Index");
            }
        }

        // POST: FaultReports/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Create(CreateFaultReportViewModel viewModel)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await _userManager.GetUserAsync(User);
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.UserId == user.Id);

                    if (customer == null)
                    {
                        TempData["ErrorMessage"] = "Customer profile not found.";
                        return RedirectToAction("CreateProfile", "Customer");
                    }

                    var faultReport = new FaultReport
                    {
                        FridgeId = viewModel.FridgeId,
                        CustomerId = customer.CustomerId,
                        Description = viewModel.Description,
                        RequestReplacement = viewModel.RequestReplacement,
                        CustomerNotes = viewModel.CustomerNotes,
                        DateReported = DateTime.UtcNow,
                        Status = FaultStatus.Reported
                    };

                    // Update fridge status to faulty
                    var fridge = await _context.Fridges.FindAsync(viewModel.FridgeId);
                    if (fridge != null)
                    {
                        fridge.Status = FridgeStatus.Faulty;
                        _context.Update(fridge);
                    }

                    _context.FaultReports.Add(faultReport);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Fault report created: ID {FaultReportId} by customer {CustomerId}",
                        faultReport.FaultReportId, customer.CustomerId);

                    TempData["SuccessMessage"] = "Fault reported successfully! A technician will contact you soon.";
                    return RedirectToAction("Index");
                }

                // Reload available fridges if model is invalid
                var userReload = await _userManager.GetUserAsync(User);
                var customerReload = await _context.Customers
                    .FirstOrDefaultAsync(c => c.UserId == userReload.Id);
                viewModel.AvailableFridges = await _context.Fridges
                    .Where(f => f.CustomerId == customerReload.CustomerId && f.Status != FridgeStatus.Scrapped)
                    .ToListAsync();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fault report");
                TempData["ErrorMessage"] = "An error occurred while reporting the fault. Please try again.";
                return View(viewModel);
            }
        }

        // GET: FaultReports/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var faultReport = await _context.FaultReports
                    .Include(fr => fr.Customer)
                    .Include(fr => fr.Fridge)
                    .Include(fr => fr.AssignedTechnician)
                    .FirstOrDefaultAsync(m => m.FaultReportId == id);

                if (faultReport == null)
                {
                    return NotFound();
                }

                // Check if user has access to this fault report
                var user = await _userManager.GetUserAsync(User);
                var userRoles = await _userManager.GetRolesAsync(user);

                if (!userRoles.Contains("FaultTechnician") && !userRoles.Contains("Administrator"))
                {
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.UserId == user.Id);

                    if (customer == null || faultReport.CustomerId != customer.CustomerId)
                    {
                        return Forbid();
                    }
                }

                return View(faultReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fault report details for ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while retrieving fault report details.";
                return RedirectToAction("Index");
            }
        }


        // GET: FaultReports/CreateFromMaintenance
        [Authorize(Roles = "MaintenanceTechnician,Administrator")]
        public async Task<IActionResult> CreateFromMaintenance(int fridgeId)
        {
            try
            {
                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .FirstOrDefaultAsync(f => f.FridgeId == fridgeId);

                if (fridge == null)
                {
                    TempData["ErrorMessage"] = "Fridge not found.";
                    return RedirectToAction("Index", "Maintenance");
                }

                var viewModel = new CreateFaultReportViewModel
                {
                    FridgeId = fridgeId,
                    Description = $"Fault discovered during maintenance on {DateTime.UtcNow:MMM dd, yyyy}",
                    AvailableFridges = new List<Fridge> { fridge }
                };

                ViewBag.FridgeDetails = $"{fridge.Model} (SN: {fridge.SerialNumber}) - {fridge.Customer?.BusinessName}";
                return View("Create", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fault report creation from maintenance");
                TempData["ErrorMessage"] = "An error occurred while loading the fault report page.";
                return RedirectToAction("Index", "Maintenance");
            }
        }

        // POST: FaultReports/CreateFromMaintenance
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "MaintenanceTechnician,Administrator")]
        public async Task<IActionResult> CreateFromMaintenance(CreateFaultReportViewModel viewModel)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    await ReloadFaultReportViewModel(viewModel);
                    return View("Create", viewModel);
                }

                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .FirstOrDefaultAsync(f => f.FridgeId == viewModel.FridgeId);

                if (fridge == null)
                {
                    TempData["ErrorMessage"] = "Fridge not found.";
                    return RedirectToAction("Index", "Maintenance");
                }

                var faultReport = new FaultReport
                {
                    FridgeId = viewModel.FridgeId,
                    CustomerId = fridge.CustomerId ?? 0,
                    Description = viewModel.Description,
                    RequestReplacement = viewModel.RequestReplacement,
                    CustomerNotes = viewModel.CustomerNotes,
                    DateReported = DateTime.UtcNow,
                    Status = FaultStatus.Reported
                };

                _context.FaultReports.Add(faultReport);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Fault report created successfully!";
                return RedirectToAction("Index", "Maintenance");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating fault report from maintenance");
                TempData["ErrorMessage"] = "An error occurred while creating the fault report.";
                await ReloadFaultReportViewModel(viewModel);
                return View("Create", viewModel);
            }
        }

        // GET: FaultReports/Update/5 - Technician only
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> Update(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var faultReport = await _context.FaultReports
                    .Include(fr => fr.Customer)
                    .Include(fr => fr.Fridge)
                    .Include(fr => fr.AssignedTechnician)
                    .FirstOrDefaultAsync(fr => fr.FaultReportId == id);

                if (faultReport == null)
                {
                    return NotFound();
                }

                var technicians = await _userManager.GetUsersInRoleAsync("FaultTechnician");

                var viewModel = new UpdateFaultReportViewModel
                {
                    FaultReportId = faultReport.FaultReportId,
                    Status = faultReport.Status,
                    DiagnosisDetails = faultReport.DiagnosisDetails,
                    ScheduledDate = faultReport.ScheduledDate,
                    AssignedTechnicianId = faultReport.AssignedTechnicianId,
                    PartsRequired = faultReport.PartsRequired,
                    RepairCost = faultReport.RepairCost,
                    InternalNotes = faultReport.InternalNotes,
                    ReplacementApproved = faultReport.ReplacementApproved ?? false,
                    AvailableTechnicians = technicians.ToList()
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fault report update form for ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the update form.";
                return RedirectToAction("Index");
            }
        }

        // POST: FaultReports/Update/5 - Technician only
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> Update(int id, UpdateFaultReportViewModel viewModel)
        {
            if (id != viewModel.FaultReportId)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var faultReport = await _context.FaultReports
                        .Include(fr => fr.Fridge)
                        .FirstOrDefaultAsync(fr => fr.FaultReportId == id);

                    if (faultReport == null)
                    {
                        return NotFound();
                    }

                    // Update fault report
                    faultReport.Status = viewModel.Status;
                    faultReport.DiagnosisDetails = viewModel.DiagnosisDetails;
                    faultReport.ScheduledDate = viewModel.ScheduledDate;
                    faultReport.AssignedTechnicianId = viewModel.AssignedTechnicianId;
                    faultReport.PartsRequired = viewModel.PartsRequired;
                    faultReport.RepairCost = viewModel.RepairCost;
                    faultReport.InternalNotes = viewModel.InternalNotes;
                    faultReport.ReplacementApproved = viewModel.ReplacementApproved;

                    // Update repair date if status is resolved
                    if (viewModel.Status == FaultStatus.Resolved || viewModel.Status == FaultStatus.Closed)
                    {
                        faultReport.RepairDate = DateTime.UtcNow;

                        // Update fridge status back to allocated
                        if (faultReport.Fridge != null)
                        {
                            faultReport.Fridge.Status = FridgeStatus.Allocated;
                            _context.Update(faultReport.Fridge);
                        }
                    }

                    _context.Update(faultReport);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Fault report updated: ID {FaultReportId} by technician", id);
                    TempData["SuccessMessage"] = "Fault report updated successfully.";

                    return RedirectToAction("Details", new { id });
                }

                // Reload technicians if model is invalid
                var technicians = await _userManager.GetUsersInRoleAsync("FaultTechnician");
                viewModel.AvailableTechnicians = technicians.ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fault report ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while updating the fault report.";
                return View(viewModel);
            }
        }

        // GET: FaultReports/MyAssigned - Technician's assigned reports
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> MyAssigned()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);

                var assignedReports = await _context.FaultReports
                    .Include(fr => fr.Customer)
                    .Include(fr => fr.Fridge)
                    .Where(fr => fr.AssignedTechnicianId == user.Id)
                    .Where(fr => fr.Status != FaultStatus.Resolved && fr.Status != FaultStatus.Closed)
                    .OrderByDescending(fr => fr.DateReported)
                    .ToListAsync();

                return View(assignedReports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving assigned fault reports");
                TempData["ErrorMessage"] = "An error occurred while retrieving your assigned reports.";
                return View(new List<FaultReport>());
            }
        }

        // POST: FaultReports/AssignToMe - Technician self-assignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> AssignToMe(int id)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var faultReport = await _context.FaultReports.FindAsync(id);

                if (faultReport == null)
                {
                    return NotFound();
                }

                faultReport.AssignedTechnicianId = user.Id;
                faultReport.Status = FaultStatus.Diagnosed; // Move to diagnosed when assigned

                _context.Update(faultReport);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Fault report {FaultReportId} assigned to technician {TechnicianId}", id, user.Id);
                TempData["SuccessMessage"] = "Fault report assigned to you successfully.";

                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error assigning fault report {FaultReportId} to technician", id);
                TempData["ErrorMessage"] = "An error occurred while assigning the fault report.";
                return RedirectToAction("Details", new { id });
            }
        }

        // GET: FaultReports/Diagnose/5 - Enhanced diagnosis form
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> Diagnose(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var faultReport = await _context.FaultReports
                    .Include(fr => fr.Customer)
                    .Include(fr => fr.Fridge)
                    .Include(fr => fr.AssignedTechnician)
                    .FirstOrDefaultAsync(fr => fr.FaultReportId == id);

                if (faultReport == null)
                {
                    return NotFound();
                }

                var user = await _userManager.GetUserAsync(User);
                
                // Check if technician is assigned to this report or is admin
                if (faultReport.AssignedTechnicianId != user.Id && !User.IsInRole("Administrator"))
                {
                    TempData["ErrorMessage"] = "You can only diagnose reports assigned to you.";
                    return RedirectToAction("Index");
                }

                var viewModel = new UpdateFaultReportViewModel
                {
                    FaultReportId = faultReport.FaultReportId,
                    Status = faultReport.Status,
                    DiagnosisDetails = faultReport.DiagnosisDetails,
                    ScheduledDate = faultReport.ScheduledDate,
                    AssignedTechnicianId = faultReport.AssignedTechnicianId,
                    PartsRequired = faultReport.PartsRequired,
                    RepairCost = faultReport.RepairCost,
                    InternalNotes = faultReport.InternalNotes,
                    ReplacementApproved = faultReport.ReplacementApproved ?? false
                };

                // Populate technicians dropdown
                var technicians = await _userManager.GetUsersInRoleAsync("FaultTechnician");
                viewModel.AvailableTechnicians = technicians.ToList();

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading diagnosis form for fault report ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the diagnosis form.";
                return RedirectToAction("Index");
            }
        }

        // POST: FaultReports/Diagnose/5 - Submit diagnosis
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> Diagnose(int id, UpdateFaultReportViewModel viewModel)
        {
            if (id != viewModel.FaultReportId)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    var faultReport = await _context.FaultReports
                        .Include(fr => fr.Customer)
                        .Include(fr => fr.Fridge)
                        .FirstOrDefaultAsync(fr => fr.FaultReportId == id);

                    if (faultReport == null)
                    {
                        return NotFound();
                    }

                    var user = await _userManager.GetUserAsync(User);
                    
                    // Check if technician is assigned to this report or is admin
                    if (faultReport.AssignedTechnicianId != user.Id && !User.IsInRole("Administrator"))
                    {
                        TempData["ErrorMessage"] = "You can only diagnose reports assigned to you.";
                        return RedirectToAction("Index");
                    }

                    // Update diagnosis details and assignment
                    faultReport.DiagnosisDetails = viewModel.DiagnosisDetails;
                    faultReport.PartsRequired = viewModel.PartsRequired;
                    faultReport.RepairCost = viewModel.RepairCost;
                    faultReport.InternalNotes = viewModel.InternalNotes;
                    faultReport.AssignedTechnicianId = viewModel.AssignedTechnicianId;
                    faultReport.Status = FaultStatus.Diagnosed;

                    // If scheduled date is provided, move to scheduled status
                    if (viewModel.ScheduledDate.HasValue)
                    {
                        faultReport.ScheduledDate = viewModel.ScheduledDate;
                        faultReport.Status = FaultStatus.Scheduled;
                    }

                    _context.Update(faultReport);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Diagnosis completed for fault report {FaultReportId} by technician {TechnicianId}", 
                        id, user.Id);

                    TempData["SuccessMessage"] = "Diagnosis completed successfully!";
                    return RedirectToAction("Details", new { id });
                }

                // Repopulate technicians on invalid model
                var technicians = await _userManager.GetUsersInRoleAsync("FaultTechnician");
                viewModel.AvailableTechnicians = technicians.ToList();
                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting diagnosis for fault report ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while submitting the diagnosis.";
                return View(viewModel);
            }
        }

        // POST: FaultReports/ScheduleRepair/5 - Schedule repair and notify customer
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> ScheduleRepair(int id, DateTime scheduledDate, string customerNotes)
        {
            try
            {
                var faultReport = await _context.FaultReports
                    .Include(fr => fr.Customer)
                    .Include(fr => fr.Fridge)
                    .FirstOrDefaultAsync(fr => fr.FaultReportId == id);

                if (faultReport == null)
                {
                    return NotFound();
                }

                var user = await _userManager.GetUserAsync(User);
                
                // Check if technician is assigned to this report or is admin
                if (faultReport.AssignedTechnicianId != user.Id && !User.IsInRole("Administrator"))
                {
                    TempData["ErrorMessage"] = "You can only schedule repairs for reports assigned to you.";
                    return RedirectToAction("Index");
                }

                // Validate scheduled date
                if (scheduledDate < DateTime.UtcNow.Date)
                {
                    TempData["ErrorMessage"] = "Scheduled date must be today or in the future.";
                    return RedirectToAction("Details", new { id });
                }

                // Update fault report
                faultReport.ScheduledDate = scheduledDate;
                faultReport.Status = FaultStatus.Scheduled;
                faultReport.InternalNotes += $"\nRepair scheduled for {scheduledDate:yyyy-MM-dd} by {user.FirstName} {user.LastName}";

                _context.Update(faultReport);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Repair scheduled for fault report {FaultReportId} on {ScheduledDate} by technician {TechnicianId}", 
                    id, scheduledDate, user.Id);

                TempData["SuccessMessage"] = $"Repair scheduled for {scheduledDate:yyyy-MM-dd}. Customer has been notified.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scheduling repair for fault report ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while scheduling the repair.";
                return RedirectToAction("Details", new { id });
            }
        }

        // POST: FaultReports/StartRepair/5 - Start repair work
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> StartRepair(int id)
        {
            try
            {
                var faultReport = await _context.FaultReports
                    .Include(fr => fr.Fridge)
                    .FirstOrDefaultAsync(fr => fr.FaultReportId == id);

                if (faultReport == null)
                {
                    return NotFound();
                }

                var user = await _userManager.GetUserAsync(User);
                
                // Check if technician is assigned to this report or is admin
                if (faultReport.AssignedTechnicianId != user.Id && !User.IsInRole("Administrator"))
                {
                    TempData["ErrorMessage"] = "You can only start repairs for reports assigned to you.";
                    return RedirectToAction("Index");
                }

                // Update status to in progress
                faultReport.Status = FaultStatus.InProgress;
                faultReport.InternalNotes += $"\nRepair work started on {DateTime.UtcNow:yyyy-MM-dd HH:mm} by {user.FirstName} {user.LastName}";

                _context.Update(faultReport);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Repair work started for fault report {FaultReportId} by technician {TechnicianId}", 
                    id, user.Id);

                TempData["SuccessMessage"] = "Repair work started successfully.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting repair for fault report ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while starting the repair.";
                return RedirectToAction("Details", new { id });
            }
        }

        // POST: FaultReports/CompleteRepair/5 - Complete repair
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireFaultTechnician")]
        public async Task<IActionResult> CompleteRepair(int id, string repairNotes)
        {
            try
            {
                var faultReport = await _context.FaultReports
                    .Include(fr => fr.Fridge)
                    .FirstOrDefaultAsync(fr => fr.FaultReportId == id);

                if (faultReport == null)
                {
                    return NotFound();
                }

                var user = await _userManager.GetUserAsync(User);
                
                // Check if technician is assigned to this report or is admin
                if (faultReport.AssignedTechnicianId != user.Id && !User.IsInRole("Administrator"))
                {
                    TempData["ErrorMessage"] = "You can only complete repairs for reports assigned to you.";
                    return RedirectToAction("Index");
                }

                // Update status to resolved
                faultReport.Status = FaultStatus.Resolved;
                faultReport.RepairDate = DateTime.UtcNow;
                faultReport.InternalNotes += $"\nRepair completed on {DateTime.UtcNow:yyyy-MM-dd HH:mm} by {user.FirstName} {user.LastName}";
                
                if (!string.IsNullOrEmpty(repairNotes))
                {
                    faultReport.InternalNotes += $"\nRepair Notes: {repairNotes}";
                }

                // Update fridge status back to allocated
                if (faultReport.Fridge != null)
                {
                    faultReport.Fridge.Status = FridgeStatus.Allocated;
                    _context.Update(faultReport.Fridge);
                }

                _context.Update(faultReport);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Repair completed for fault report {FaultReportId} by technician {TechnicianId}", 
                    id, user.Id);

                TempData["SuccessMessage"] = "Repair completed successfully! Fridge is now operational.";
                return RedirectToAction("Details", new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing repair for fault report ID {FaultReportId}", id);
                TempData["ErrorMessage"] = "An error occurred while completing the repair.";
                return RedirectToAction("Details", new { id });
            }
        }

        private async Task ReloadFaultReportViewModel(CreateFaultReportViewModel viewModel)
        {
            var user = await _userManager.GetUserAsync(User);
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (customer != null)
            {
                // Customer context - load their fridges
                viewModel.AvailableFridges = await _context.Fridges
                    .Where(f => f.CustomerId == customer.CustomerId && f.Status != FridgeStatus.Scrapped)
                    .OrderBy(f => f.Model)
                    .ToListAsync();
            }
            else if (viewModel.FridgeId > 0)
            {
                // Maintenance context - load the specific fridge
                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .FirstOrDefaultAsync(f => f.FridgeId == viewModel.FridgeId);
                
                if (fridge != null)
                {
                    viewModel.AvailableFridges = new List<Fridge> { fridge };
                }
            }
        }
    }
}