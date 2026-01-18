using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace GRP_03_27.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdminCustomersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<AdminCustomersController> _logger;

        public AdminCustomersController(ApplicationDbContext context, UserManager<User> userManager, ILogger<AdminCustomersController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: AdminCustomers with search and filter
        public async Task<IActionResult> Index(string searchString, string customerType, string status, int page = 1, int pageSize = 10)
        {
            try
            {
                var customers = _context.Customers
                    .Include(c => c.Fridges)
                    .AsQueryable();

                // Search filter
                if (!string.IsNullOrEmpty(searchString))
                {
                    customers = customers.Where(c =>
                        c.BusinessName.Contains(searchString) ||
                        c.ContactPerson.Contains(searchString) ||
                        c.Email.Contains(searchString) ||
                        c.PhoneNumber.Contains(searchString));
                }

                // Customer type filter
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

                // Pagination
                var totalCount = await customers.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

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
                _logger.LogError(ex, "Error retrieving customers");
                TempData["ErrorMessage"] = "An error occurred while retrieving customers.";
                return View(new List<Customer>());
            }
        }

        // GET: AdminCustomers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var customer = await _context.Customers
                .Include(c => c.Fridges)
                    .ThenInclude(f => f.FaultReports)
                .Include(c => c.Fridges)
                    .ThenInclude(f => f.MaintenanceRecords)
                .Include(c => c.FaultReports)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null)
            {
                return NotFound();
            }

            return View(customer);
        }

        // GET: AdminCustomers/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: AdminCustomers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate email
                    if (await _context.Customers.AnyAsync(c => c.Email == customer.Email))
                    {
                        ModelState.AddModelError("Email", "A customer with this email already exists.");
                        return View(customer);
                    }

                    customer.RegistrationDate = DateTime.UtcNow;
                    customer.LastUpdated = DateTime.UtcNow;
                    customer.IsActive = true;

                    _context.Add(customer);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Customer created: {BusinessName} (ID: {CustomerId})", customer.BusinessName, customer.CustomerId);
                    TempData["SuccessMessage"] = $"Customer '{customer.BusinessName}' created successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating customer");
                ModelState.AddModelError("", "Unable to save changes. Please try again.");
            }
            return View(customer);
        }

        // Administrators can only view customer details and create new customers
        // Customers must manage their own personal details and account status

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.CustomerId == id);
        }
    }
}
