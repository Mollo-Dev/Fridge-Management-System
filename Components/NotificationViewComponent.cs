using GRP_03_27.Models;
using GRP_03_27.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GRP_03_27.Components
{
    public class NotificationViewComponent : ViewComponent
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<User> _userManager;

        public NotificationViewComponent(INotificationService notificationService, UserManager<User> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = _userManager.GetUserId(HttpContext.User);
            if (string.IsNullOrEmpty(userId))
            {
                return Content(string.Empty);
            }

            var notifications = await _notificationService.GetNotificationsForUserAsync(userId, 5);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            var model = new NotificationViewModel
            {
                Notifications = notifications,
                UnreadCount = unreadCount
            };

            return View(model);
        }
    }

    public class NotificationViewModel
    {
        public List<Notification> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
    }
}
