using GRP_03_27.Data;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminLocationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AdminLocationsController> _logger;

        public AdminLocationsController(ApplicationDbContext context, ILogger<AdminLocationsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: AdminLocations
        public async Task<IActionResult> Index(string searchString, int page = 1, int pageSize = 10)
        {
            try
            {
                var locations = _context.Locations
                    .Include(l => l.Customer)
                    .AsQueryable();

                // Search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    locations = locations.Where(l =>
                        l.Address.Contains(searchString) ||
                        l.GPSCoordinates.Contains(searchString) ||
                        l.Customer.BusinessName.Contains(searchString));
                }

                var totalCount = await locations.CountAsync();
                var pagedLocations = await locations
                    .OrderBy(l => l.Customer.BusinessName)
                    .ThenBy(l => l.Address)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                ViewBag.SearchString = searchString;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalCount = totalCount;
                ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

                return View(pagedLocations);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading locations");
                TempData["ErrorMessage"] = "An error occurred while loading locations.";
                return View(new List<Location>());
            }
        }

        // GET: AdminLocations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(m => m.LocationId == id);

            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        // GET: AdminLocations/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.BusinessName)
                .Select(c => new { c.CustomerId, c.BusinessName })
                .ToListAsync();
            return View();
        }

        // POST: AdminLocations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Location location)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(location);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Location created: {Address} (ID: {LocationId})", location.Address, location.LocationId);
                    TempData["SuccessMessage"] = $"Location '{location.Address}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating location");
                ModelState.AddModelError("", "Unable to save changes. Please try again.");
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.BusinessName)
                .Select(c => new { c.CustomerId, c.BusinessName })
                .ToListAsync();
            return View(location);
        }

        // GET: AdminLocations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations.FindAsync(id);
            if (location == null)
            {
                return NotFound();
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.BusinessName)
                .Select(c => new { c.CustomerId, c.BusinessName })
                .ToListAsync();
            return View(location);
        }

        // POST: AdminLocations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Location location)
        {
            if (id != location.LocationId)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    _context.Update(location);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Location updated: {Address} (ID: {LocationId})", location.Address, location.LocationId);
                    TempData["SuccessMessage"] = $"Location '{location.Address}' updated successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating location {LocationId}", id);
                ModelState.AddModelError("", "Unable to save changes. Please try again.");
            }

            ViewBag.Customers = await _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.BusinessName)
                .Select(c => new { c.CustomerId, c.BusinessName })
                .ToListAsync();
            return View(location);
        }

        // GET: AdminLocations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var location = await _context.Locations
                .Include(l => l.Customer)
                .FirstOrDefaultAsync(m => m.LocationId == id);

            if (location == null)
            {
                return NotFound();
            }

            return View(location);
        }

        // POST: AdminLocations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var location = await _context.Locations.FindAsync(id);
                if (location != null)
                {
                    _context.Locations.Remove(location);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Location deleted: {Address} (ID: {LocationId})", location.Address, location.LocationId);
                    TempData["SuccessMessage"] = $"Location '{location.Address}' deleted successfully.";
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting location {LocationId}", id);
                TempData["ErrorMessage"] = "Unable to delete location. Please try again.";
            }
            return RedirectToAction(nameof(Index));
        }

        private bool LocationExists(int id)
        {
            return _context.Locations.Any(e => e.LocationId == id);
        }
    }
}
