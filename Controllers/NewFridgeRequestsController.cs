using GRP_03_27.Data;
using GRP_03_27.Models;
using GRP_03_27.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace GRP_03_27.Controllers
{
    [Authorize(Roles = "CustomerLiaison")]
    public class NewFridgeRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NewFridgeRequestsController> _logger;

        public NewFridgeRequestsController(ApplicationDbContext context, ILogger<NewFridgeRequestsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: NewFridgeRequests
        public async Task<IActionResult> Index(string? status, string? priority, string? search)
        {
            var query = _context.NewFridgeRequests
                .Include(nfr => nfr.Customer)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<NewFridgeRequestStatus>(status, out var statusEnum))
            {
                query = query.Where(nfr => nfr.Status == statusEnum);
            }

            // Filter by priority
            if (!string.IsNullOrEmpty(priority) && Enum.TryParse<PriorityLevel>(priority, out var priorityEnum))
            {
                query = query.Where(nfr => nfr.Priority == priorityEnum);
            }

            // Search functionality
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(nfr => 
                    nfr.Customer.BusinessName.Contains(search) ||
                    nfr.BusinessJustification.Contains(search) ||
                    nfr.AdditionalNotes.Contains(search));
            }

            var requests = await query
                .OrderByDescending(nfr => nfr.RequestDate)
                .ToListAsync();

            ViewBag.StatusFilter = status;
            ViewBag.PriorityFilter = priority;
            ViewBag.SearchTerm = search;

            return View(requests);
        }

        // GET: NewFridgeRequests/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var newFridgeRequest = await _context.NewFridgeRequests
                .Include(nfr => nfr.Customer)
                .FirstOrDefaultAsync(m => m.NewFridgeRequestId == id);

            if (newFridgeRequest == null)
            {
                return NotFound();
            }

            return View(newFridgeRequest);
        }

        // GET: NewFridgeRequests/Review/5
        public async Task<IActionResult> Review(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var newFridgeRequest = await _context.NewFridgeRequests
                .Include(nfr => nfr.Customer)
                .FirstOrDefaultAsync(m => m.NewFridgeRequestId == id);

            if (newFridgeRequest == null)
            {
                return NotFound();
            }

            return View(newFridgeRequest);
        }

        // POST: NewFridgeRequests/Approve/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, int approvedQuantity, string? approvalNotes)
        {
            var newFridgeRequest = await _context.NewFridgeRequests.FindAsync(id);
            if (newFridgeRequest == null)
            {
                return NotFound();
            }

            try
            {
                newFridgeRequest.Status = NewFridgeRequestStatus.Approved;
                newFridgeRequest.ApprovedQuantity = approvedQuantity;
                newFridgeRequest.ApprovalDate = DateTime.UtcNow;
                newFridgeRequest.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
                newFridgeRequest.ApprovalNotes = approvalNotes;

                _context.Update(newFridgeRequest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Fridge request #{id} has been approved for {approvedQuantity} fridges.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving fridge request {RequestId}", id);
                TempData["ErrorMessage"] = "An error occurred while approving the request.";
                return RedirectToAction(nameof(Review), new { id });
            }
        }

        // POST: NewFridgeRequests/Reject/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? rejectionReason)
        {
            var newFridgeRequest = await _context.NewFridgeRequests.FindAsync(id);
            if (newFridgeRequest == null)
            {
                return NotFound();
            }

            try
            {
                newFridgeRequest.Status = NewFridgeRequestStatus.Rejected;
                newFridgeRequest.ApprovalDate = DateTime.UtcNow;
                newFridgeRequest.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
                newFridgeRequest.ApprovalNotes = rejectionReason;

                _context.Update(newFridgeRequest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Fridge request #{id} has been rejected.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting fridge request {RequestId}", id);
                TempData["ErrorMessage"] = "An error occurred while rejecting the request.";
                return RedirectToAction(nameof(Review), new { id });
            }
        }

        // GET: NewFridgeRequests/Allocate/5
        public async Task<IActionResult> Allocate(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var newFridgeRequest = await _context.NewFridgeRequests
                .Include(nfr => nfr.Customer)
                .FirstOrDefaultAsync(m => m.NewFridgeRequestId == id);

            if (newFridgeRequest == null)
            {
                return NotFound();
            }

            // Get available fridges for allocation
            var availableFridges = await _context.Fridges
                .Where(f => f.Status == FridgeStatus.Available)
                .OrderBy(f => f.SerialNumber)
                .ToListAsync();

            ViewBag.AvailableFridges = availableFridges;
            return View(newFridgeRequest);
        }

        // POST: NewFridgeRequests/Allocate/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Allocate(int id, int[] selectedFridgeIds, string? allocationNotes)
        {
            var newFridgeRequest = await _context.NewFridgeRequests
                .Include(nfr => nfr.Customer)
                .FirstOrDefaultAsync(nfr => nfr.NewFridgeRequestId == id);

            if (newFridgeRequest == null)
            {
                return NotFound();
            }

            try
            {
                // Validate that we have enough fridges
                if (selectedFridgeIds.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please select at least one fridge to allocate.";
                    return RedirectToAction(nameof(Allocate), new { id });
                }

                if (selectedFridgeIds.Length > (newFridgeRequest.ApprovedQuantity ?? newFridgeRequest.Quantity))
                {
                    TempData["ErrorMessage"] = "Cannot allocate more fridges than approved quantity.";
                    return RedirectToAction(nameof(Allocate), new { id });
                }

                // Allocate the selected fridges
                foreach (var fridgeId in selectedFridgeIds)
                {
                    var fridge = await _context.Fridges.FindAsync(fridgeId);
                    if (fridge != null && fridge.Status == FridgeStatus.Available)
                    {
                        fridge.Status = FridgeStatus.Allocated;
                        fridge.CustomerId = newFridgeRequest.CustomerId;
                        fridge.AllocationDate = DateTime.UtcNow;

                        // Create allocation history record
                        var allocationHistory = new AllocationHistory
                        {
                            FridgeId = fridgeId,
                            CustomerId = newFridgeRequest.CustomerId,
                            Action = AllocationAction.Allocated,
                            ActionDate = DateTime.UtcNow,
                            ActionById = User.FindFirstValue(ClaimTypes.NameIdentifier),
                            Notes = allocationNotes ?? $"Allocated via fridge request #{id}"
                        };

                        _context.AllocationHistories.Add(allocationHistory);
                    }
                }

                // Update the request status
                newFridgeRequest.Status = NewFridgeRequestStatus.Allocated;
                newFridgeRequest.AllocationDate = DateTime.UtcNow;
                newFridgeRequest.AllocatedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
                newFridgeRequest.AllocationNotes = allocationNotes;

                _context.Update(newFridgeRequest);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Successfully allocated {selectedFridgeIds.Length} fridges for request #{id}.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error allocating fridges for request {RequestId}", id);
                TempData["ErrorMessage"] = "An error occurred while allocating the fridges.";
                return RedirectToAction(nameof(Allocate), new { id });
            }
        }

        // GET: NewFridgeRequests/Statistics
        public async Task<IActionResult> Statistics()
        {
            var stats = new
            {
                TotalRequests = await _context.NewFridgeRequests.CountAsync(),
                PendingRequests = await _context.NewFridgeRequests.CountAsync(nfr => nfr.Status == NewFridgeRequestStatus.Pending),
                UnderReviewRequests = await _context.NewFridgeRequests.CountAsync(nfr => nfr.Status == NewFridgeRequestStatus.UnderReview),
                ApprovedRequests = await _context.NewFridgeRequests.CountAsync(nfr => nfr.Status == NewFridgeRequestStatus.Approved),
                AllocatedRequests = await _context.NewFridgeRequests.CountAsync(nfr => nfr.Status == NewFridgeRequestStatus.Allocated),
                CompletedRequests = await _context.NewFridgeRequests.CountAsync(nfr => nfr.Status == NewFridgeRequestStatus.Completed),
                RejectedRequests = await _context.NewFridgeRequests.CountAsync(nfr => nfr.Status == NewFridgeRequestStatus.Rejected),
                OverdueRequests = await _context.NewFridgeRequests.CountAsync(nfr => nfr.IsOverdue),
                AverageProcessingTime = await _context.NewFridgeRequests
                    .Where(nfr => nfr.Status == NewFridgeRequestStatus.Completed && nfr.AllocationDate.HasValue)
                    .Select(nfr => EF.Functions.DateDiffDay(nfr.RequestDate, nfr.AllocationDate.Value))
                    .DefaultIfEmpty(0)
                    .AverageAsync()
            };

            return View(stats);
        }
    }
}
