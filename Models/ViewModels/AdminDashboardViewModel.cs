using GRP_03_27.Models;
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace GRP_03_27.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalCustomers { get; set; }
        public int TotalFridges { get; set; }
        public int TotalSuppliers { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveFaultReports { get; set; }
        public int ScheduledMaintenance { get; set; }
        public int PendingPurchaseRequests { get; set; }
        public List<Customer> RecentCustomers { get; set; } = new List<Customer>();
        public List<FaultReport> RecentFaultReports { get; set; } = new List<FaultReport>();
    }

    public class UserViewModel
    {
        public User User { get; set; }
        public IList<string> Roles { get; set; }
    }

    public class RoleViewModel
    {
        public IdentityRole Role { get; set; }
        public int UserCount { get; set; }
    }
}
