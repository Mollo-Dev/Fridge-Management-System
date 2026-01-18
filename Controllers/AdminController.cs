using GRP_03_27.Data;
using GRP_03_27.Models;
using GRP_03_27.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        // GET: Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            try
            {
                var dashboardData = new AdminDashboardViewModel
                {
                    TotalCustomers = await _context.Customers.CountAsync(),
                    TotalFridges = await _context.Fridges.CountAsync(),
                    TotalSuppliers = await _context.Suppliers.CountAsync(),
                    TotalUsers = await _userManager.Users.CountAsync(),
                    ActiveFaultReports = await _context.FaultReports.CountAsync(f => f.Status != Enums.FaultStatus.Resolved),
                    ScheduledMaintenance = await _context.MaintenanceRecords.CountAsync(m => m.Status == Enums.MaintenanceStatus.Scheduled),
                    PendingPurchaseRequests = await _context.PurchaseRequests.CountAsync(p => p.Status == Enums.PurchaseRequestStatus.Pending),
                    RecentCustomers = await _context.Customers
                        .OrderByDescending(c => c.RegistrationDate)
                        .Take(5)
                        .ToListAsync(),
                    RecentFaultReports = await _context.FaultReports
                        .Include(f => f.Fridge)
                        .ThenInclude(f => f.Customer)
                        .OrderByDescending(f => f.DateReported)
                        .Take(5)
                        .ToListAsync()
                };

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                TempData["ErrorMessage"] = "An error occurred while loading the dashboard.";
                return View(new AdminDashboardViewModel());
            }
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users(string searchString, string roleFilter, int page = 1, int pageSize = 10)
        {
            try
            {
                var users = _userManager.Users.AsQueryable();

                // Search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    users = users.Where(u =>
                        u.FirstName.Contains(searchString) ||
                        u.LastName.Contains(searchString) ||
                        u.Email.Contains(searchString) ||
                        u.EmployeeId.Contains(searchString));
                }

                // Role filter
                if (!string.IsNullOrEmpty(roleFilter))
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(roleFilter);
                    var userIds = usersInRole.Select(u => u.Id);
                    users = users.Where(u => userIds.Contains(u.Id));
                }

                var totalCount = await users.CountAsync();
                var pagedUsers = await users
                    .OrderBy(u => u.FirstName)
                    .ThenBy(u => u.LastName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Get roles for each user
                var usersWithRoles = new List<dynamic>();
                foreach (var user in pagedUsers)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    usersWithRoles.Add(new
                    {
                        User = user,
                        Roles = roles
                    });
                }

                ViewBag.SearchString = searchString;
                ViewBag.RoleFilter = roleFilter;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                // Get available roles for filter dropdown
                ViewBag.AvailableRoles = await _roleManager.Roles.ToListAsync();

                return View(usersWithRoles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users");
                TempData["ErrorMessage"] = "An error occurred while loading users.";
                return View(new List<dynamic>());
            }
        }

        // GET: Admin/Roles
        public async Task<IActionResult> Roles()
        {
            try
            {
                var roles = await _roleManager.Roles.ToListAsync();
                var rolesWithUserCounts = new List<dynamic>();

                foreach (var role in roles)
                {
                    var userCount = await _userManager.GetUsersInRoleAsync(role.Name);
                    rolesWithUserCounts.Add(new
                    {
                        Role = role,
                        UserCount = userCount.Count
                    });
                }

                return View(rolesWithUserCounts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading roles");
                TempData["ErrorMessage"] = "An error occurred while loading roles.";
                return View(new List<dynamic>());
            }
        }

        // GET: Admin/SystemHealth
        public async Task<IActionResult> SystemHealth()
        {
            try
            {
                var healthData = new
                {
                    DatabaseStatus = "Connected",
                    TotalRecords = new
                    {
                        Customers = await _context.Customers.CountAsync(),
                        Fridges = await _context.Fridges.CountAsync(),
                        Suppliers = await _context.Suppliers.CountAsync(),
                        Users = await _userManager.Users.CountAsync(),
                        FaultReports = await _context.FaultReports.CountAsync(),
                        MaintenanceRecords = await _context.MaintenanceRecords.CountAsync(),
                        PurchaseRequests = await _context.PurchaseRequests.CountAsync()
                    },
                    SystemMetrics = new
                    {
                        ActiveUsers = await _userManager.Users.CountAsync(u => u.IsActive),
                        InactiveUsers = await _userManager.Users.CountAsync(u => !u.IsActive),
                        OverdueMaintenance = await _context.MaintenanceRecords.CountAsync(m => m.ScheduledDate < DateTime.UtcNow && m.Status == Enums.MaintenanceStatus.Scheduled),
                        HighPriorityFaults = await _context.FaultReports.CountAsync(f => f.Priority == Enums.PriorityLevel.High)
                    },
                    LastUpdated = DateTime.UtcNow
                };

                return View(healthData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading system health");
                TempData["ErrorMessage"] = "An error occurred while loading system health.";
                return View();
            }
        }

        // GET: Admin/AuditLogs
        public async Task<IActionResult> AuditLogs(DateTime? dateFrom, DateTime? dateTo, int page = 1, int pageSize = 20)
        {
            try
            {
                // For now, return a placeholder view
                // In a real application, you would implement proper audit logging
                var auditData = new
                {
                    Logs = new List<dynamic>(),
                    DateFrom = dateFrom ?? DateTime.UtcNow.AddDays(-30),
                    DateTo = dateTo ?? DateTime.UtcNow,
                    CurrentPage = page,
                    PageSize = pageSize,
                    TotalCount = 0,
                    TotalPages = 0
                };

                return View(auditData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading audit logs");
                TempData["ErrorMessage"] = "An error occurred while loading audit logs.";
                return View();
            }
        }

        // GET: Admin/Reports
        public async Task<IActionResult> Reports()
        {
            try
            {
                var reportData = new
                {
                    CustomerReports = new
                    {
                        TotalCustomers = await _context.Customers.CountAsync(),
                        ActiveCustomers = await _context.Customers.CountAsync(c => c.IsActive),
                        CustomersByType = await _context.Customers
                            .GroupBy(c => c.Type)
                            .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                            .ToListAsync()
                    },
                    FridgeReports = new
                    {
                        TotalFridges = await _context.Fridges.CountAsync(),
                        FridgesByStatus = await _context.Fridges
                            .GroupBy(f => f.Status)
                            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                            .ToListAsync(),
                        FridgesBySupplier = await _context.Fridges
                            .Include(f => f.Supplier)
                            .GroupBy(f => f.Supplier.Name)
                            .Select(g => new { Supplier = g.Key, Count = g.Count() })
                            .ToListAsync()
                    },
                    MaintenanceReports = new
                    {
                        TotalMaintenance = await _context.MaintenanceRecords.CountAsync(),
                        MaintenanceByStatus = await _context.MaintenanceRecords
                            .GroupBy(m => m.Status)
                            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                            .ToListAsync(),
                        MaintenanceByType = await _context.MaintenanceRecords
                            .GroupBy(m => m.MaintenanceType)
                            .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                            .ToListAsync()
                    },
                    FaultReports = new
                    {
                        TotalFaults = await _context.FaultReports.CountAsync(),
                        FaultsByStatus = await _context.FaultReports
                            .GroupBy(f => f.Status)
                            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                            .ToListAsync(),
                        FaultsByPriority = await _context.FaultReports
                            .GroupBy(f => f.Priority)
                            .Select(g => new { Priority = g.Key.ToString(), Count = g.Count() })
                            .ToListAsync()
                    }
                };

                return View(reportData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading reports");
                TempData["ErrorMessage"] = "An error occurred while loading reports.";
                return View();
            }
        }

        // GET: Admin/Backup
        public IActionResult Backup()
        {
            try
            {
                var backupData = new
                {
                    LastBackup = DateTime.UtcNow.AddDays(-1), // Placeholder
                    BackupSize = "2.5 GB",
                    BackupLocation = "/backups/fridgemanagement_20250120.bak",
                    NextScheduledBackup = DateTime.UtcNow.AddDays(1),
                    BackupStatus = "Success"
                };

                return View(backupData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading backup information");
                TempData["ErrorMessage"] = "An error occurred while loading backup information.";
                return View();
            }
        }

        // GET: Admin/Search
        [HttpGet]
        public async Task<IActionResult> Search(string query, int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                {
                    return Json(new { results = new List<object>() });
                }

                var searchResults = new List<object>();

                // Search customers
                var customers = await _context.Customers
                    .Where(c => c.BusinessName.Contains(query) || 
                               c.ContactPerson.Contains(query) || 
                               c.Email.Contains(query) || 
                               c.PhoneNumber.Contains(query))
                    .Take(limit)
                    .Select(c => new
                    {
                        type = "Customer",
                        id = c.CustomerId,
                        title = c.BusinessName,
                        subtitle = c.ContactPerson,
                        description = $"{c.Email} • {c.PhoneNumber}",
                        url = Url.Action("Details", "AdminCustomers", new { id = c.CustomerId }),
                        icon = "bi-people",
                        status = c.IsActive ? "Active" : "Inactive",
                        statusClass = c.IsActive ? "success" : "danger"
                    })
                    .ToListAsync();

                searchResults.AddRange(customers);

                // Search fridges
                var fridges = await _context.Fridges
                    .Include(f => f.Customer)
                    .Include(f => f.Supplier)
                    .Where(f => f.SerialNumber.Contains(query) || 
                              f.Model.Contains(query) || 
                              f.Customer.BusinessName.Contains(query))
                    .Take(limit)
                    .Select(f => new
                    {
                        type = "Fridge",
                        id = f.FridgeId,
                        title = f.SerialNumber,
                        subtitle = f.Model,
                        description = $"{f.Customer.BusinessName} • {f.Supplier.Name}",
                        url = Url.Action("Details", "Fridges", new { id = f.FridgeId }),
                        icon = "bi-refrigerator",
                        status = f.Status.ToString(),
                        statusClass = f.Status == Enums.FridgeStatus.Available ? "success" : 
                                    f.Status == Enums.FridgeStatus.Allocated ? "primary" : 
                                    f.Status == Enums.FridgeStatus.UnderMaintenance ? "warning" : "danger"
                    })
                    .ToListAsync();

                searchResults.AddRange(fridges);

                // Search users
                var users = await _userManager.Users
                    .Where(u => u.FirstName.Contains(query) || 
                               u.LastName.Contains(query) || 
                               u.Email.Contains(query) || 
                               u.EmployeeId.Contains(query))
                    .Take(limit)
                    .Select(u => new
                    {
                        type = "User",
                        id = u.Id,
                        title = $"{u.FirstName} {u.LastName}",
                        subtitle = u.Email,
                        description = $"Employee ID: {u.EmployeeId}",
                        url = Url.Action("Users", "Admin"),
                        icon = "bi-person-gear",
                        status = u.IsActive ? "Active" : "Inactive",
                        statusClass = u.IsActive ? "success" : "danger"
                    })
                    .ToListAsync();

                searchResults.AddRange(users);

                // Search fault reports
                var faultReports = await _context.FaultReports
                    .Include(f => f.Fridge)
                    .ThenInclude(f => f.Customer)
                    .Where(f => f.Description.Contains(query) || 
                               (f.Fridge != null && f.Fridge.SerialNumber.Contains(query)) || 
                               (f.Fridge != null && f.Fridge.Customer != null && f.Fridge.Customer.BusinessName.Contains(query)))
                    .Take(limit)
                    .Select(f => new
                    {
                        type = "Fault Report",
                        id = f.FaultReportId,
                        title = $"Fault #{f.FaultReportId}",
                        subtitle = f.Fridge != null && f.Fridge.Customer != null ? f.Fridge.Customer.BusinessName : "New Fridge Request",
                        description = f.Description.Length > 100 ? f.Description.Substring(0, 100) + "..." : f.Description,
                        url = Url.Action("Details", "FaultReports", new { id = f.FaultReportId }),
                        icon = "bi-exclamation-triangle",
                        status = f.Status.ToString(),
                        statusClass = f.Status == Enums.FaultStatus.Resolved ? "success" : 
                                    f.Status == Enums.FaultStatus.InProgress ? "warning" : "danger"
                    })
                    .ToListAsync();

                searchResults.AddRange(faultReports);

                // Search maintenance records
                var maintenanceRecords = await _context.MaintenanceRecords
                    .Include(m => m.Fridge)
                    .ThenInclude(f => f.Customer)
                    .Where(m => m.TechnicianNotes.Contains(query) || 
                               m.Fridge.SerialNumber.Contains(query) || 
                               m.Fridge.Customer.BusinessName.Contains(query))
                    .Take(limit)
                    .Select(m => new
                    {
                        type = "Maintenance",
                        id = m.MaintenanceRecordId,
                        title = $"Maintenance #{m.MaintenanceRecordId}",
                        subtitle = m.Fridge.Customer.BusinessName,
                        description = m.TechnicianNotes.Length > 100 ? m.TechnicianNotes.Substring(0, 100) + "..." : m.TechnicianNotes,
                        url = Url.Action("Details", "Maintenance", new { id = m.MaintenanceRecordId }),
                        icon = "bi-tools",
                        status = m.Status.ToString(),
                        statusClass = m.Status == Enums.MaintenanceStatus.Completed ? "success" : 
                                    m.Status == Enums.MaintenanceStatus.InProgress ? "warning" : "primary"
                    })
                    .ToListAsync();

                searchResults.AddRange(maintenanceRecords);

                // Search suppliers
                var suppliers = await _context.Suppliers
                    .Where(s => s.Name.Contains(query) || 
                               s.Email.Contains(query) || 
                               s.ContactNumber.Contains(query))
                    .Take(limit)
                    .Select(s => new
                    {
                        type = "Supplier",
                        id = s.SupplierId,
                        title = s.Name,
                        subtitle = s.Email,
                        description = $"{s.Email} • {s.ContactNumber}",
                        url = Url.Action("Details", "AdminSuppliers", new { id = s.SupplierId }),
                        icon = "bi-truck",
                        status = "Active",
                        statusClass = "success"
                    })
                    .ToListAsync();

                searchResults.AddRange(suppliers);

                // Group results by type and limit total results
                var groupedResults = searchResults
                    .GroupBy(r => r.GetType().GetProperty("type").GetValue(r).ToString())
                    .ToDictionary(g => g.Key, g => g.Take(5).ToList());

                return Json(new { 
                    results = searchResults.Take(20).ToList(),
                    grouped = groupedResults,
                    totalCount = searchResults.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing live search with query: {Query}", query);
                return Json(new { results = new List<object>(), error = "Search failed" });
            }
        }

        // POST: Admin/ToggleUserStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound();
                }

                user.IsActive = !user.IsActive;
                var result = await _userManager.UpdateAsync(user);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = $"User {(user.IsActive ? "activated" : "deactivated")} successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to update user status.";
                }

                return RedirectToAction(nameof(Users));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling user status for user {UserId}", userId);
                TempData["ErrorMessage"] = "An error occurred while updating user status.";
                return RedirectToAction(nameof(Users));
            }
        }
    }
}
