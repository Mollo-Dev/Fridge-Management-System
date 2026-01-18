using GRP_03_27.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace GRP_03_27.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<GRP_03_27.Models.User> _userManager;

        public HomeController(UserManager<GRP_03_27.Models.User> userManager)
        {
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Dashboard()
        {
            var user = await _userManager.GetUserAsync(User);

            if (await _userManager.IsInRoleAsync(user, "Administrator"))
                return RedirectToAction("Dashboard", "Admin");

            if (await _userManager.IsInRoleAsync(user, "FaultTechnician"))
                return RedirectToAction("Dashboard", "FaultReports");

            if (await _userManager.IsInRoleAsync(user, "MaintenanceTechnician"))
                return RedirectToAction("Dashboard", "Maintenance");

            if (await _userManager.IsInRoleAsync(user, "CustomerLiaison"))
                return RedirectToAction("Dashboard", "Customers");

            if (await _userManager.IsInRoleAsync(user, "InventoryLiaison"))
                return RedirectToAction("InventoryReport", "Fridges");

            if (await _userManager.IsInRoleAsync(user, "PurchasingManager"))
                return RedirectToAction("Index", "PurchaseRequests");

            if (await _userManager.IsInRoleAsync(user, "Customer"))
                return RedirectToAction("Dashboard", "Customer");

            return RedirectToAction("Index");
        }
    }
}