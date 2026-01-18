using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Components
{
    public class CustomerNotificationViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly UserManager<User> _userManager;

        public CustomerNotificationViewComponent(ApplicationDbContext context,
                                               IHttpContextAccessor httpContextAccessor,
                                               UserManager<User> userManager)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(_httpContextAccessor.HttpContext.User);
            if (user == null)
                return Content(string.Empty);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (customer == null)
                return Content(string.Empty);

            var activeFaultsCount = await _context.Fridges
                .CountAsync(f => f.CustomerId == customer.CustomerId &&
                               f.FaultReports.Any(fr =>
                                   fr.Status != FaultStatus.Resolved &&
                                   fr.Status != FaultStatus.Closed));

            var pendingReportsCount = await _context.FaultReports
                .CountAsync(fr => fr.CustomerId == customer.CustomerId &&
                                (fr.Status == FaultStatus.Reported ||
                                 fr.Status == FaultStatus.Diagnosed));

            var model = new CustomerNotificationViewModel
            {
                ActiveFaultsCount = activeFaultsCount,
                PendingReportsCount = pendingReportsCount
            };

            return View(model);
        }
    }

    public class CustomerNotificationViewModel
    {
        public int ActiveFaultsCount { get; set; }
        public int PendingReportsCount { get; set; }
    }
}