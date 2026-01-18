using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace GRP_03_27.Controllers
{
    [Authorize(Policy = "RequireCustomerLiaison")]
    public class CustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ApplicationDbContext context, UserManager<User> userManager, ILogger<CustomersController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Customers with search and filter
        public async Task<IActionResult> Index(string searchString, string customerType, string status, int page = 1, int pageSize = 10)
        {
            try
            {
                // Validate pagination parameters
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 5, 50); // Reasonable limits

                var customers = _context.Customers
                    .Include(c => c.Fridges)
                    .AsNoTracking()
                    .AsQueryable();

                // Sanitize and apply search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    var sanitizedSearch = SanitizeInput(searchString);
                    if (!string.IsNullOrEmpty(sanitizedSearch) && sanitizedSearch.Length >= 2)
                    {
                        customers = customers.Where(c =>
                            c.BusinessName.Contains(sanitizedSearch) ||
                            c.ContactPerson.Contains(sanitizedSearch) ||
                            c.Email.Contains(sanitizedSearch) ||
                            c.PhoneNumber.Contains(sanitizedSearch));
                    }
                }

                // Customer type filter with validation
                if (!string.IsNullOrEmpty(customerType) && Enum.TryParse<CustomerType>(customerType, out var type))
                {
                    customers = customers.Where(c => c.Type == type);
                }

                // Status filter
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Active")
                        customers = customers.Where(c => c.IsActive);
                    else if (status == "Inactive")
                        customers = customers.Where(c => !c.IsActive);
                }

                // Pagination with performance considerations
                var totalCount = await customers.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                // Ensure page is within bounds
                page = Math.Min(page, Math.Max(1, totalPages));

                var customerList = await customers
                    .OrderBy(c => c.BusinessName)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.SearchString = searchString;
                ViewBag.CustomerType = customerType;
                ViewBag.Status = status;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;

                ViewBag.CustomerTypes = new SelectList(Enum.GetValues(typeof(CustomerType)));
                ViewBag.StatusOptions = new SelectList(new[] { "All", "Active", "Inactive" });

                return View(customerList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers for page {Page} with search '{Search}'", page, searchString);
                TempData["ErrorMessage"] = "An error occurred while retrieving customers. Please try again.";
                return View(new List<Customer>());
            }
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    _logger.LogWarning("Invalid customer ID requested: {CustomerId}", id);
                    return NotFound();
                }

                var customer = await _context.Customers
                    .Include(c => c.Fridges)
                        .ThenInclude(f => f.FaultReports)
                    .Include(c => c.Fridges)
                        .ThenInclude(f => f.MaintenanceRecords)
                    .Include(c => c.FaultReports)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.CustomerId == id);

                if (customer == null)
                {
                    _logger.LogWarning("Customer not found with ID: {CustomerId}", id);
                    return NotFound();
                }

                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer details for ID: {CustomerId}", id);
                TempData["ErrorMessage"] = "An error occurred while retrieving customer details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Customers/ManageFridges/5
        public async Task<IActionResult> ManageFridges(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    return NotFound();
                }

                var customer = await _context.Customers
                    .Include(c => c.Fridges)
                        .ThenInclude(f => f.Supplier)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (customer == null)
                {
                    return NotFound();
                }

                var availableFridges = await _context.Fridges
                    .Where(f => f.Status == FridgeStatus.Available)
                    .Include(f => f.Supplier)
                    .AsNoTracking()
                    .ToListAsync();

                ViewBag.AvailableFridges = new SelectList(availableFridges, "FridgeId", "DisplayName");
                return View(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading fridge management for customer ID: {CustomerId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading fridge management.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Customers/AllocateFridge
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllocateFridge(int customerId, int fridgeId)
        {
            try
            {
                // Validate inputs
                if (customerId <= 0 || fridgeId <= 0)
                {
                    _logger.LogWarning("Invalid allocation parameters - CustomerId: {CustomerId}, FridgeId: {FridgeId}", customerId, fridgeId);
                    return BadRequest("Invalid allocation parameters.");
                }

                var customer = await _context.Customers.FindAsync(customerId);
                var fridge = await _context.Fridges.FindAsync(fridgeId);
                var currentUser = await _userManager.GetUserAsync(User);

                if (customer == null || fridge == null)
                {
                    _logger.LogWarning("Customer or fridge not found during allocation - Customer: {CustomerId}, Fridge: {FridgeId}", customerId, fridgeId);
                    return NotFound("Customer or fridge not found.");
                }

                // Business logic validation
                if (!customer.IsActive)
                {
                    TempData["ErrorMessage"] = "Cannot allocate fridges to inactive customers.";
                    return RedirectToAction(nameof(ManageFridges), new { id = customerId });
                }

                if (fridge.Status != FridgeStatus.Available)
                {
                    TempData["ErrorMessage"] = "Selected fridge is not available for allocation.";
                    return RedirectToAction(nameof(ManageFridges), new { id = customerId });
                }

                // Check if fridge is already allocated
                if (fridge.CustomerId.HasValue)
                {
                    TempData["ErrorMessage"] = "This fridge is already allocated to another customer.";
                    return RedirectToAction(nameof(ManageFridges), new { id = customerId });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Update fridge allocation
                    fridge.CustomerId = customerId;
                    fridge.Status = FridgeStatus.Allocated;
                    fridge.LastUpdated = DateTime.UtcNow;
                    _context.Update(fridge);

                    // Update customer last updated
                    customer.LastUpdated = DateTime.UtcNow;
                    _context.Update(customer);

                    // Create allocation history record
                    var allocationHistory = new AllocationHistory
                    {
                        FridgeId = fridgeId,
                        CustomerId = customerId,
                        Action = AllocationAction.Allocated,
                        ActionDate = DateTime.UtcNow,
                        ActionById = currentUser?.Id ?? "System",
                        Notes = $"Fridge allocated to {customer.BusinessName}"
                    };
                    _context.AllocationHistories.Add(allocationHistory);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Fridge {FridgeId} successfully allocated to customer {CustomerId} by user {UserId}",
                        fridgeId, customerId, currentUser?.Id);
                    TempData["SuccessMessage"] = $"Fridge '{fridge.Model}' allocated to '{customer.BusinessName}' successfully.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during fridge allocation - Fridge: {FridgeId}, Customer: {CustomerId}", fridgeId, customerId);
                    throw;
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error allocating fridge {FridgeId} to customer {CustomerId}", fridgeId, customerId);
                TempData["ErrorMessage"] = "A database error occurred while allocating the fridge. Please try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error allocating fridge {FridgeId} to customer {CustomerId}", fridgeId, customerId);
                TempData["ErrorMessage"] = "An unexpected error occurred while allocating the fridge.";
            }

            return RedirectToAction(nameof(ManageFridges), new { id = customerId });
        }

        // POST: Customers/DeallocateFridge
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeallocateFridge(int customerId, int fridgeId)
        {
            try
            {
                if (customerId <= 0 || fridgeId <= 0)
                {
                    return BadRequest("Invalid deallocation parameters.");
                }

                var fridge = await _context.Fridges.FindAsync(fridgeId);
                var customer = await _context.Customers.FindAsync(customerId);
                var currentUser = await _userManager.GetUserAsync(User);

                if (fridge == null || customer == null)
                {
                    return NotFound("Fridge or customer not found.");
                }

                // Validate ownership
                if (fridge.CustomerId != customerId)
                {
                    TempData["ErrorMessage"] = "This fridge is not allocated to the specified customer.";
                    return RedirectToAction(nameof(ManageFridges), new { id = customerId });
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Update fridge allocation
                    fridge.CustomerId = null;
                    fridge.Status = FridgeStatus.Available;
                    fridge.LastUpdated = DateTime.UtcNow;
                    _context.Update(fridge);

                    // Update customer last updated
                    customer.LastUpdated = DateTime.UtcNow;
                    _context.Update(customer);

                    // Create allocation history record
                    var allocationHistory = new AllocationHistory
                    {
                        FridgeId = fridgeId,
                        CustomerId = customerId,
                        Action = AllocationAction.Deallocated,
                        ActionDate = DateTime.UtcNow,
                        ActionById = currentUser?.Id ?? "System",
                        Notes = $"Fridge deallocated from {customer.BusinessName}"
                    };
                    _context.AllocationHistories.Add(allocationHistory);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Fridge {FridgeId} deallocated from customer {CustomerId}", fridgeId, customerId);
                    TempData["SuccessMessage"] = "Fridge deallocated successfully.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during fridge deallocation");
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deallocating fridge {FridgeId} from customer {CustomerId}", fridgeId, customerId);
                TempData["ErrorMessage"] = "An error occurred while deallocating the fridge.";
            }

            return RedirectToAction(nameof(ManageFridges), new { id = customerId });
        }

        // GET: Customers/AllocationHistory/5
        public async Task<IActionResult> AllocationHistory(int? id, int page = 1, int pageSize = 10)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    return NotFound();
                }

                var customer = await _context.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.CustomerId == id);

                if (customer == null)
                {
                    return NotFound();
                }

                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 5, 50);

                var allocationHistoryQuery = _context.AllocationHistories
                    .Include(ah => ah.Fridge)
                    .Include(ah => ah.ActionBy)
                    .Where(ah => ah.CustomerId == id)
                    .AsNoTracking();

                var totalCount = await allocationHistoryQuery.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                page = Math.Min(page, Math.Max(1, totalPages));

                var allocationHistory = await allocationHistoryQuery
                    .OrderByDescending(ah => ah.ActionDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.Customer = customer;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalCount = totalCount;

                return View(allocationHistory);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading allocation history for customer ID: {CustomerId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading allocation history.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Customers/Dashboard
        public IActionResult Dashboard()
        {
            return View();
        }

        // GET: Customers/DashboardMetrics
        [HttpGet]
        public async Task<IActionResult> DashboardMetrics()
        {
            try
            {
                var metrics = new
                {
                    totalCustomers = await _context.Customers.CountAsync(),
                    activeCustomers = await _context.Customers.CountAsync(c => c.IsActive),
                    totalFridges = await _context.Fridges.CountAsync(f => f.Status != FridgeStatus.Scrapped),
                    availableFridges = await _context.Fridges.CountAsync(f => f.Status == FridgeStatus.Available),
                    allocatedFridges = await _context.Fridges.CountAsync(f => f.Status == FridgeStatus.Allocated),
                    inventoryStatus = new
                    {
                        available = await _context.Fridges.CountAsync(f => f.Status == FridgeStatus.Available),
                        allocated = await _context.Fridges.CountAsync(f => f.Status == FridgeStatus.Allocated),
                        underMaintenance = await _context.Fridges.CountAsync(f => f.Status == FridgeStatus.UnderMaintenance),
                        faulty = await _context.Fridges.CountAsync(f => f.Status == FridgeStatus.Faulty)
                    },
                    customerTypes = await _context.Customers
                        .Where(c => c.IsActive)
                        .GroupBy(c => c.Type)
                        .Select(g => new { type = g.Key.ToString(), count = g.Count() })
                        .ToListAsync(),
                    recentActivity = await _context.AllocationHistories
                        .Include(ah => ah.Customer)
                        .Include(ah => ah.Fridge)
                        .OrderByDescending(ah => ah.ActionDate)
                        .Take(5)
                        .Select(ah => new
                        {
                            action = ah.Action.ToString(),
                            customer = ah.Customer.BusinessName,
                            fridge = ah.Fridge.Model,
                            date = ah.ActionDate.ToString("MMM dd, yyyy HH:mm")
                        })
                        .ToListAsync()
                };

                return Json(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard metrics");
                return Json(new { error = "Failed to load dashboard metrics" });
            }
        }

        // GET: Customers/RecentAllocations
        [HttpGet]
        public async Task<IActionResult> RecentAllocations(int count = 10)
        {
            try
            {
                count = Math.Clamp(count, 1, 50); // Prevent excessive data loading

                var recentAllocations = await _context.AllocationHistories
                    .Include(ah => ah.Fridge)
                    .Include(ah => ah.Customer)
                    .Include(ah => ah.ActionBy)
                    .Where(ah => ah.Action == AllocationAction.Allocated || ah.Action == AllocationAction.Deallocated)
                    .OrderByDescending(ah => ah.ActionDate)
                    .Take(count)
                    .Select(ah => new
                    {
                        actionDate = ah.ActionDate.ToString("MMM dd, HH:mm"),
                        description = ah.ActionDescription,
                        fridgeModel = ah.Fridge.Model,
                        customerName = ah.Customer.BusinessName,
                        actionBy = ah.ActionBy != null ? ah.ActionBy.UserName : "System"
                    })
                    .ToListAsync();

                return Json(recentAllocations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent allocations");
                return Json(new { error = "Failed to load recent allocations" });
            }
        }

        #region Helper Methods

        private string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove potentially dangerous characters but allow reasonable search terms
            return Regex.Replace(input.Trim(), @"[<>""']", string.Empty);
        }

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }

        #endregion
    }
}