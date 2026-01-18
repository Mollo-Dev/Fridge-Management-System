//using GRP_03_27.Data;
//using GRP_03_27.Enums;
//using Microsoft.AspNetCore.Identity;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;

//namespace GRP_03_27.Components
//{
//    public class FaultBadgeViewComponent : ViewComponent
//    {
//        private readonly ApplicationDbContext _context;
//        private readonly UserManager<User> _userManager;

//        public FaultBadgeViewComponent(ApplicationDbContext context, UserManager<User> userManager)
//        {
//            _context = context;
//            _userManager = userManager;
//        }

//        public async Task<IViewComponentResult> InvokeAsync()
//        {
//            var user = await _userManager.GetUserAsync(HttpContext.User);
//            if (user == null) return Content(string.Empty);

//            var urgentCount = await _context.FaultReports
//                .CountAsync(fr => (fr.Status == FaultStatus.Reported || fr.Status == FaultStatus.Diagnosed) &&
//                                 (DateTime.UtcNow - fr.DateReported).TotalDays > 7);

//            if (urgentCount > 0)
//            {
//                return View("Default", urgentCount);
//            }

//            return Content(string.Empty);
//        }
//    }
//}