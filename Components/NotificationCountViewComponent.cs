using GRP_03_27.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace GRP_03_27.Components
{
    public class NotificationCountViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public NotificationCountViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Get the current user's ID
            var userId = HttpContext.User.Identity?.Name;
            
            if (string.IsNullOrEmpty(userId))
            {
                return Content("0");
            }

            // Count unread notifications for the current user
            // This is a simple implementation - you can customize based on your notification logic
            int count = await _context.Notifications
                .Where(n => !n.IsRead)
                .CountAsync();

            return Content(count > 0 ? count.ToString() : "0");
        }
    }
}

