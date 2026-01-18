using GRP_03_27.Data;
using GRP_03_27.Enums;
using GRP_03_27.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Components
{
    public class CustomerFaultBadgeViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CustomerFaultBadgeViewComponent(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            if (user == null) return Content(string.Empty);

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == user.Id);

            if (customer == null) return Content(string.Empty);

            var activeFaultsCount = await _context.FaultReports
                .CountAsync(fr => fr.CustomerId == customer.CustomerId &&
                                 fr.Status != FaultStatus.Resolved &&
                                 fr.Status != FaultStatus.Closed);

            if (activeFaultsCount > 0)
            {
                return View("Default", activeFaultsCount);
            }

            return Content(string.Empty);
        }
    }
}