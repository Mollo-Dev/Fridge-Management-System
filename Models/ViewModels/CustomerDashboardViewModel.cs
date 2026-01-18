namespace GRP_03_27.Models.ViewModels
{
    public class CustomerDashboardViewModel
    {
        public Customer Customer { get; set; }
        public int TotalFridges { get; set; }
        public int ActiveFridges { get; set; }
        public int FaultyFridges { get; set; }
        public int TotalFaultReports { get; set; }
        public int PendingFaultReports { get; set; }
        public int InProgressFaultReports { get; set; }
        public int ResolvedFaultReports { get; set; }
        public List<Fridge> Fridges { get; set; } = new List<Fridge>();
        public List<FaultReport> RecentFaultReports { get; set; } = new List<FaultReport>();
        public List<FaultReport> ActiveFaultReports { get; set; } = new List<FaultReport>();
    }
}