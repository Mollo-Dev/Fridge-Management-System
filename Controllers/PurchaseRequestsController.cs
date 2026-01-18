using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using GRP_03_27.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Controllers
{
    [Authorize]
    public class PurchaseRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly ILogger<PurchaseRequestsController> _logger;
        private readonly INotificationService _notificationService;

        public PurchaseRequestsController(ApplicationDbContext context, UserManager<User> userManager, 
            ILogger<PurchaseRequestsController> logger, INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _notificationService = notificationService;
        }

        // GET: PurchaseRequests
        [Authorize(Policy = "RequireInventoryLiaison")]
        public async Task<IActionResult> Index()
        {
            try
            {
                var purchaseRequests = await _context.PurchaseRequests
                    .Include(pr => pr.RequestedBy)
                    .OrderByDescending(pr => pr.RequestDate)
                    .ToListAsync();
                return View(purchaseRequests);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase requests");
                TempData["ErrorMessage"] = "An error occurred while retrieving purchase requests.";
                return View(new List<PurchaseRequest>());
            }
        }

        // GET: PurchaseRequests/Create
        [Authorize(Policy = "RequireInventoryLiaison")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: PurchaseRequests/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireInventoryLiaison")]
        public async Task<IActionResult> Create([Bind("Quantity,Reason")] PurchaseRequest purchaseRequest)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await _userManager.GetUserAsync(User);
                    purchaseRequest.RequestedById = user.Id;
                    purchaseRequest.RequestDate = DateTime.UtcNow;
                    purchaseRequest.Status = PurchaseRequestStatus.Pending;

                    _context.Add(purchaseRequest);
                    await _context.SaveChangesAsync();

                    // Create automatic notification
                    await _notificationService.CreatePurchaseRequestNotificationAsync(purchaseRequest);

                    TempData["SuccessMessage"] = "Purchase request submitted successfully.";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error creating purchase request");
                ModelState.AddModelError("", "Unable to save changes. Please try again.");
            }
            return View(purchaseRequest);
        }

        // GET: PurchaseRequests/Approve/5
        [Authorize(Policy = "RequirePurchasingManager")]
        public async Task<IActionResult> Approve(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var purchaseRequest = await _context.PurchaseRequests
                .Include(pr => pr.RequestedBy)
                .FirstOrDefaultAsync(pr => pr.PurchaseRequestId == id);

            if (purchaseRequest == null)
            {
                return NotFound();
            }

            return View(purchaseRequest);
        }

        // POST: PurchaseRequests/Approve/5
        [HttpPost, ActionName("Approve")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequirePurchasingManager")]
        public async Task<IActionResult> ApproveConfirmed(int id)
        {
            var purchaseRequest = await _context.PurchaseRequests.FindAsync(id);
            if (purchaseRequest == null)
            {
                return NotFound();
            }

            try
            {
                purchaseRequest.Status = PurchaseRequestStatus.Approved;
                _context.Update(purchaseRequest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Purchase request approved successfully.";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error approving purchase request");
                TempData["ErrorMessage"] = "An error occurred while approving the purchase request.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}