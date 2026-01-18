using GRP_03_27.Data;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminSuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminSuppliersController> _logger;

        public AdminSuppliersController(ApplicationDbContext context, ILogger<AdminSuppliersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: AdminSuppliers
        public async Task<IActionResult> Index(string searchString, int page = 1, int pageSize = 10)
        {
            try
            {
                var suppliers = _context.Suppliers
                    .Include(s => s.Fridges)
                    .AsQueryable();

                // Search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    suppliers = suppliers.Where(s =>
                        s.Name.Contains(searchString) ||
                        s.Email.Contains(searchString) ||
                        s.ContactNumber.Contains(searchString) ||
                        s.Address.Contains(searchString));
                }

                var totalCount = await suppliers.CountAsync();
                var pagedSuppliers = await suppliers
                    .OrderBy(s => s.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.SearchString = searchString;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                return View(pagedSuppliers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading suppliers");
                TempData["ErrorMessage"] = "An error occurred while loading suppliers.";
                return View(new List<Supplier>());
            }
        }

        // GET: AdminSuppliers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .Include(s => s.Fridges)
                    .ThenInclude(f => f.Customer)
                .Include(s => s.PurchaseRequests)
                .FirstOrDefaultAsync(m => m.SupplierId == id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // GET: AdminSuppliers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminSuppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate email
                    if (await _context.Suppliers.AnyAsync(s => s.Email == supplier.Email))
                    {
                        ModelState.AddModelError("Email", "A supplier with this email already exists.");
                        return View(supplier);
                    }

                    _context.Add(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier created: {SupplierName} (ID: {SupplierId})", supplier.Name, supplier.SupplierId);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating supplier");
                ModelState.AddModelError("", "Unable to save changes. Please try again.");
            }
            return View(supplier);
        }

        // GET: AdminSuppliers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        // POST: AdminSuppliers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier)
        {
            if (id != supplier.SupplierId)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate email (excluding current supplier)
                    if (await _context.Suppliers.AnyAsync(s => s.Email == supplier.Email && s.SupplierId != id))
                    {
                        ModelState.AddModelError("Email", "A supplier with this email already exists.");
                        return View(supplier);
                    }

                    _context.Update(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier updated: {SupplierName} (ID: {SupplierId})", supplier.Name, supplier.SupplierId);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating supplier {SupplierId}", id);
                ModelState.AddModelError("", "Unable to save changes. Please try again.");
            }
            return View(supplier);
        }

        // GET: AdminSuppliers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var supplier = await _context.Suppliers
                .Include(s => s.Fridges)
                .Include(s => s.PurchaseRequests)
                .FirstOrDefaultAsync(m => m.SupplierId == id);

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        // POST: AdminSuppliers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier != null)
                {
                    // Check if supplier has associated fridges
                    var hasFridges = await _context.Fridges.AnyAsync(f => f.SupplierId == id);

                    if (hasFridges)
                    {
                        TempData["ErrorMessage"] = $"Cannot delete supplier '{supplier.Name}' because they have associated fridges.";
                        return RedirectToAction(nameof(Index));
                    }

                    _context.Suppliers.Remove(supplier);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Supplier deleted: {SupplierName} (ID: {SupplierId})", supplier.Name, supplier.SupplierId);
                    TempData["SuccessMessage"] = $"Supplier '{supplier.Name}' deleted successfully.";
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting supplier {SupplierId}", id);
                TempData["ErrorMessage"] = "Unable to delete supplier. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.SupplierId == id);
        }
    }
}
