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
    [Authorize(Roles = "Administrator,InventoryLiaison")]
    public class FridgesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FridgesController> _logger;
        private readonly UserManager<User> _userManager;

        public FridgesController(ApplicationDbContext context, ILogger<FridgesController> logger, UserManager<User> userManager)
        {
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // GET: Fridges with comprehensive filtering
        public async Task<IActionResult> Index(string searchString, string statusFilter, string supplierFilter, int page = 1, int pageSize = 10)
        {
            try
            {
                // Validate pagination parameters
                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 5, 50);

                var fridges = _context.Fridges
                    .Include(f => f.Customer)
                    .Include(f => f.Supplier)
                    .AsNoTracking()
                    .AsQueryable();

                // Sanitize and apply search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    var sanitizedSearch = SanitizeInput(searchString);
                    if (!string.IsNullOrEmpty(sanitizedSearch) && sanitizedSearch.Length >= 2)
                    {
                        fridges = fridges.Where(f =>
                            f.Model.Contains(sanitizedSearch) ||
                            f.SerialNumber.Contains(sanitizedSearch) ||
                            (f.Customer != null && f.Customer.BusinessName.Contains(sanitizedSearch)));
                    }
                }

                // Status filter
                if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
                {
                    if (Enum.TryParse<FridgeStatus>(statusFilter, out var status))
                    {
                        fridges = fridges.Where(f => f.Status == status);
                    }
                }

                // Supplier filter
                if (!string.IsNullOrEmpty(supplierFilter) && supplierFilter != "All")
                {
                    if (int.TryParse(supplierFilter, out var supplierId))
                    {
                        fridges = fridges.Where(f => f.SupplierId == supplierId);
                    }
                }

                // Pagination
                var totalCount = await fridges.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
                page = Math.Min(page, Math.Max(1, totalPages));

                var fridgeList = await fridges
                    .OrderBy(f => f.Status)
                    .ThenBy(f => f.Model)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Ensure fridges have proper supplier relationships
                foreach (var fridge in fridgeList)
                {
                    if (fridge.Supplier == null)
                    {
                        fridge.Supplier = await _context.Suppliers.FindAsync(fridge.SupplierId);
                    }
                }

                ViewBag.SearchString = searchString;
                ViewBag.StatusFilter = statusFilter;
                ViewBag.SupplierFilter = supplierFilter;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;

                ViewBag.StatusOptions = new SelectList(Enum.GetValues(typeof(FridgeStatus)).Cast<FridgeStatus>().Prepend(FridgeStatus.Available), statusFilter);
                ViewBag.SupplierOptions = new SelectList(await _context.Suppliers.ToListAsync(), "SupplierId", "Name", supplierFilter);

                // Check stock levels and create purchase request if needed (background check)
                _ = Task.Run(async () => await CheckAndCreatePurchaseRequest());

                return View(fridgeList);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving fridges for page {Page}", page);
                TempData["ErrorMessage"] = "An error occurred while retrieving fridges.";
                return View(new List<Fridge>());
            }
        }

        // GET: Fridges/InventoryReport
        public async Task<IActionResult> InventoryReport()
        {
            try
            {
                var fridges = await _context.Fridges.ToListAsync();

                var inventoryStats = new
                {
                    TotalFridges = fridges.Count,
                    AvailableFridges = fridges.Count(f => f.Status == FridgeStatus.Available),
                    AllocatedFridges = fridges.Count(f => f.Status == FridgeStatus.Allocated),
                    FaultyFridges = fridges.Count(f => f.Status == FridgeStatus.Faulty),
                    UnderMaintenanceFridges = fridges.Count(f => f.Status == FridgeStatus.UnderMaintenance),
                    ScrappedFridges = fridges.Count(f => f.Status == FridgeStatus.Scrapped),
                    LowStock = fridges.Count(f => f.Status == FridgeStatus.Available) < 5
                };

                ViewBag.InventoryStats = inventoryStats;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating inventory report");
                TempData["ErrorMessage"] = "An error occurred while generating the inventory report.";
                return View();
            }
        }

        // GET: Fridges/Scrap/5
        public async Task<IActionResult> Scrap(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    return NotFound();
                }

                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .Include(f => f.Supplier)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FridgeId == id);

                if (fridge == null)
                {
                    return NotFound();
                }

                return View(fridge);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading scrap page for fridge {FridgeId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the scrap page.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Fridges/Scrap/5 - FIXED VERSION
        [HttpPost, ActionName("Scrap")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ScrapConfirmed(int id, string scrapReason)
        {
            try
            {
                if (id <= 0)
                {
                    return BadRequest("Invalid fridge ID.");
                }

                if (string.IsNullOrWhiteSpace(scrapReason) || scrapReason.Length < 5)
                {
                    TempData["ErrorMessage"] = "Please provide a valid scrap reason (minimum 5 characters).";
                    return RedirectToAction(nameof(Scrap), new { id });
                }

                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .FirstOrDefaultAsync(f => f.FridgeId == id);

                var currentUser = await _userManager.GetUserAsync(User);

                if (fridge == null)
                {
                    return NotFound();
                }

                // Business validation - cannot scrap allocated fridge without deallocation
                if (fridge.Status == FridgeStatus.Allocated && fridge.CustomerId.HasValue)
                {
                    TempData["ErrorMessage"] = "Cannot scrap an allocated fridge. Please deallocate it first.";
                    return RedirectToAction(nameof(Index));
                }

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var previousCustomerId = fridge.CustomerId;
                    var previousStatus = fridge.Status;

                    fridge.Status = FridgeStatus.Scrapped;
                    fridge.CustomerId = null;
                    fridge.Notes += $"\nScrapped on {DateTime.UtcNow:yyyy-MM-dd}. Reason: {scrapReason}";
                    fridge.LastUpdated = DateTime.UtcNow; // Now this will work

                    _context.Update(fridge);

                    // Create allocation history record for scrapping
                    var allocationHistory = new AllocationHistory
                    {
                        FridgeId = id,
                        CustomerId = previousCustomerId,
                        Action = AllocationAction.Scrapped,
                        ActionDate = DateTime.UtcNow,
                        ActionById = currentUser?.Id ?? "System",
                        Notes = $"Fridge scrapped. Previous status: {previousStatus}. Reason: {scrapReason}"
                    };
                    _context.AllocationHistories.Add(allocationHistory);

                    await _context.SaveChangesAsync();

                    // Check stock levels and create purchase request if needed
                    await CheckAndCreatePurchaseRequest();

                    await transaction.CommitAsync();

                    _logger.LogInformation("Fridge {FridgeId} scrapped. Reason: {ScrapReason}", id, scrapReason);
                    TempData["SuccessMessage"] = "Fridge marked as scrapped successfully.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during fridge scrapping");
                    throw;
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error scrapping fridge {FridgeId}", id);
                TempData["ErrorMessage"] = "A database error occurred while scrapping the fridge.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scrapping fridge {FridgeId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while scrapping the fridge.";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Fridges/ReceiveNew - Show supplier selection
        public async Task<IActionResult> ReceiveNew()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .AsNoTracking()
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                if (!suppliers.Any())
                {
                    TempData["ErrorMessage"] = "No suppliers found. Please add suppliers first.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Suppliers = new SelectList(suppliers, "SupplierId", "Name");
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading receive new page");
                TempData["ErrorMessage"] = "An error occurred while loading the page.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Fridges/ReceiveNew/5 - Show form for specific supplier
        public async Task<IActionResult> ReceiveNewFromSupplier(int supplierId)
        {
            try
            {
                if (supplierId <= 0)
                {
                    return BadRequest("Invalid supplier ID.");
                }

                var supplier = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

                if (supplier == null)
                {
                    return NotFound();
                }

                ViewBag.Supplier = supplier;
                return View("ReceiveNewForm");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading receive new page for supplier {SupplierId}", supplierId);
                TempData["ErrorMessage"] = "An error occurred while loading the page.";
                return RedirectToAction(nameof(ReceiveNew));
            }
        }

        // POST: Fridges/ReceiveNew - Handle supplier selection
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveNew(int supplierId)
        {
            if (supplierId <= 0)
            {
                ModelState.AddModelError("", "Please select a supplier.");
                var suppliers = await _context.Suppliers
                    .AsNoTracking()
                    .OrderBy(s => s.Name)
                    .ToListAsync();
                ViewBag.Suppliers = new SelectList(suppliers, "SupplierId", "Name");
                return View();
            }

            return RedirectToAction(nameof(ReceiveNewFromSupplier), new { supplierId });
        }

        // POST: Fridges/ReceiveNewForm - Handle fridge reception
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveNewForm(int supplierId, string model, string serialNumber, int quantity)
        {
            try
            {
                // Comprehensive input validation
                if (supplierId <= 0)
                {
                    return BadRequest("Invalid supplier ID.");
                }

                if (string.IsNullOrWhiteSpace(model) || model.Length < 2 || model.Length > 100)
                {
                    ModelState.AddModelError("model", "Model must be between 2 and 100 characters.");
                }

                if (string.IsNullOrWhiteSpace(serialNumber) || serialNumber.Length < 3 || serialNumber.Length > 45)
                {
                    ModelState.AddModelError("serialNumber", "Serial number must be between 3 and 45 characters.");
                }

                if (quantity <= 0 || quantity > 100)
                {
                    ModelState.AddModelError("quantity", "Quantity must be between 1 and 100.");
                }

                // Check if supplier exists
                var supplierObj = await _context.Suppliers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

                if (supplierObj == null)
                {
                    ModelState.AddModelError("", "Selected supplier does not exist.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Supplier = supplierObj;
                    return View("ReceiveNewForm");
                }

                var currentUser = await _userManager.GetUserAsync(User);

                // Check for duplicate serial numbers
                var existingSerials = await _context.Fridges
                    .Where(f => f.SerialNumber.StartsWith(serialNumber))
                    .Select(f => f.SerialNumber)
                    .ToListAsync();

                var fridges = new List<Fridge>();
                using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    for (int i = 0; i < quantity; i++)
                    {
                        var newSerialNumber = $"{serialNumber}-{i + 1:000}";

                        // Ensure unique serial number
                        if (existingSerials.Contains(newSerialNumber))
                        {
                            _logger.LogWarning("Duplicate serial number detected: {SerialNumber}", newSerialNumber);
                            continue; // Skip this one
                        }

                        var fridge = new Fridge
                        {
                            Model = model.Trim(),
                            SerialNumber = newSerialNumber,
                            PurchaseDate = DateTime.UtcNow,
                            Status = FridgeStatus.Available,
                            SupplierId = supplierId,
                            Notes = $"Received from {supplierObj.Name} on {DateTime.UtcNow:yyyy-MM-dd}",
                            LastUpdated = DateTime.UtcNow // Now this will work
                        };

                        _context.Fridges.Add(fridge);
                        fridges.Add(fridge);
                    }

                    await _context.SaveChangesAsync();

                    // Create allocation history records for received fridges
                    foreach (var fridge in fridges)
                    {
                        var allocationHistory = new AllocationHistory
                        {
                            FridgeId = fridge.FridgeId,
                            Action = AllocationAction.Received,
                            ActionDate = DateTime.UtcNow,
                            ActionById = currentUser?.Id ?? "System",
                            Notes = $"Received from {supplierObj.Name}. Model: {model}"
                        };
                        _context.AllocationHistories.Add(allocationHistory);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Successfully received {Quantity} new fridges from {Supplier}", fridges.Count, supplierObj.Name);
                    TempData["SuccessMessage"] = $"{fridges.Count} new fridges received from {supplierObj.Name} and added to inventory.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during fridge reception");
                    throw;
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error receiving new fridges from supplier {SupplierId}", supplierId);
                TempData["ErrorMessage"] = "A database error occurred while receiving new fridges.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving new fridges from supplier {SupplierId}", supplierId);
                TempData["ErrorMessage"] = "An unexpected error occurred while receiving new fridges.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    return NotFound();
                }

                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .Include(f => f.Supplier)
                    .Include(f => f.AllocationHistories)
                        .ThenInclude(ah => ah.ActionBy)
                    .Include(f => f.AllocationHistories)
                        .ThenInclude(ah => ah.Customer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FridgeId == id);

                if (fridge == null)
                {
                    return NotFound();
                }

                return View(fridge);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading details for fridge {FridgeId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading fridge details.";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Fridges/Allocate/5
        [Authorize(Policy = "RequireInventoryLiaison")]
        public async Task<IActionResult> Allocate(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    return NotFound();
                }

                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .Include(f => f.Supplier)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.FridgeId == id);

                if (fridge == null)
                {
                    return NotFound();
                }

                if (fridge.Status != FridgeStatus.Available)
                {
                    TempData["ErrorMessage"] = "Only available fridges can be allocated.";
                    return RedirectToAction(nameof(Index));
                }

                ViewBag.Customers = new SelectList(await _context.Customers.ToListAsync(), "CustomerId", "BusinessName");
                return View(fridge);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading allocation page for fridge {FridgeId}", id);
                TempData["ErrorMessage"] = "An error occurred while loading the allocation page.";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Fridges/Allocate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireInventoryLiaison")]
        public async Task<IActionResult> Allocate(int id, int customerId, string allocationNotes)
        {
            try
            {
                if (id <= 0 || customerId <= 0)
                {
                    return BadRequest("Invalid fridge or customer ID.");
                }

                var fridge = await _context.Fridges
                    .Include(f => f.Customer)
                    .FirstOrDefaultAsync(f => f.FridgeId == id);

                if (fridge == null)
                {
                    return NotFound();
                }

                var customer = await _context.Customers.FindAsync(customerId);
                if (customer == null)
                {
                    ModelState.AddModelError("", "Selected customer does not exist.");
                }

                if (fridge.Status != FridgeStatus.Available)
                {
                    ModelState.AddModelError("", "Only available fridges can be allocated.");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Customers = new SelectList(await _context.Customers.ToListAsync(), "CustomerId", "BusinessName", customerId);
                    return View(fridge);
                }

                var currentUser = await _userManager.GetUserAsync(User);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // Update fridge status and allocation
                    var previousStatus = fridge.Status;
                    fridge.Status = FridgeStatus.Allocated;
                    fridge.CustomerId = customerId;
                    fridge.Notes += $"\nAllocated to {customer.BusinessName} on {DateTime.UtcNow:yyyy-MM-dd}. Notes: {allocationNotes}";
                    fridge.LastUpdated = DateTime.UtcNow;

                    _context.Update(fridge);

                    // Create allocation history record
                    var allocationHistory = new AllocationHistory
                    {
                        FridgeId = id,
                        CustomerId = customerId,
                        Action = AllocationAction.Allocated,
                        ActionDate = DateTime.UtcNow,
                        ActionById = currentUser?.Id ?? "System",
                        Notes = allocationNotes
                    };
                    _context.AllocationHistories.Add(allocationHistory);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Fridge {FridgeId} allocated to customer {CustomerId}", id, customerId);
                    TempData["SuccessMessage"] = $"Fridge successfully allocated to {customer.BusinessName}.";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Transaction failed during fridge allocation");
                    throw;
                }
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error allocating fridge {FridgeId}", id);
                TempData["ErrorMessage"] = "A database error occurred while allocating the fridge.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error allocating fridge {FridgeId}", id);
                TempData["ErrorMessage"] = "An unexpected error occurred while allocating the fridge.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task CreatePurchaseRequest(string reason)
        {
            try
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser == null) return;

                // Check if there's already a pending purchase request for low stock
                var existingRequest = await _context.PurchaseRequests
                    .FirstOrDefaultAsync(pr => pr.Status == PurchaseRequestStatus.Pending && 
                                              pr.Reason.Contains("Low stock"));

                if (existingRequest != null)
                {
                    _logger.LogInformation("Purchase request already exists for low stock");
                    return;
                }

                var purchaseRequest = new PurchaseRequest
                {
                    RequestedById = currentUser.Id,
                    RequestDate = DateTime.UtcNow,
                    Quantity = 10,
                    Reason = reason,
                    Priority = PriorityLevel.High,
                    Status = PurchaseRequestStatus.Pending,
                    EstimatedCost = 5000.00m
                };

                _context.PurchaseRequests.Add(purchaseRequest);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Auto-generated purchase request created for low stock");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating automatic purchase request");
            }
        }

        // Method to check and create purchase requests automatically
        private async Task CheckAndCreatePurchaseRequest()
        {
            try
            {
                var availableFridges = await _context.Fridges.CountAsync(f => f.Status == FridgeStatus.Available);
                
                if (availableFridges < 3)
                {
                    await CreatePurchaseRequest($"Low stock alert: Only {availableFridges} fridges available. Automatic restock required.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking stock levels for purchase request");
            }
        }

        private string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return Regex.Replace(input.Trim(), @"[<>""']", string.Empty);
        }
    }
}