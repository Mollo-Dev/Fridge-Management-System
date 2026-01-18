using GRP_03_27.Enums;
using System.ComponentModel.DataAnnotations;

namespace GRP_03_27.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Business name is required")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "Business name must be between 2 and 200 characters")]
        [Display(Name = "Business Name")]
        [RegularExpression(@"^[a-zA-Z0-9\s&.,'-]+$", ErrorMessage = "Business name contains invalid characters")]
        public string BusinessName { get; set; }

        [Required(ErrorMessage = "Contact person is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Contact person name must be between 2 and 100 characters")]
        [Display(Name = "Contact Person")]
        [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Contact person name can only contain letters, spaces, hyphens, and apostrophes")]
        public string ContactPerson { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(15, MinimumLength = 10, ErrorMessage = "Phone number must be between 10 and 15 characters")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        [RegularExpression(@"^(\+27|0)[1-8][0-9]{8}$", ErrorMessage = "Please enter a valid South African phone number")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Physical address is required")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Address must be between 10 and 500 characters")]
        [Display(Name = "Physical Address")]
        public string PhysicalAddress { get; set; }

        [Required(ErrorMessage = "Customer type is required")]
        [Display(Name = "Business Type")]
        public CustomerType Type { get; set; } = CustomerType.SpazaShop;

        [StringLength(100, ErrorMessage = "Custom business type cannot exceed 100 characters")]
        [Display(Name = "Custom Business Type")]
        public string? OtherBusinessType { get; set; }

        [Display(Name = "Registration Date")]
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Last Updated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [Display(Name = "Active Status")]
        public bool IsActive { get; set; } = true;

        // Enhanced validation properties
        [Display(Name = "Email Verified")]
        public bool IsEmailVerified { get; set; } = false;

        [Display(Name = "Phone Verified")]
        public bool IsPhoneVerified { get; set; } = false;

        [StringLength(50)]
        public string? VerificationToken { get; set; }

        public DateTime? TokenExpiry { get; set; }

        // Link to ASP.NET Identity User
        public string? UserId { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual ICollection<Fridge> Fridges { get; set; } = new List<Fridge>();
        public virtual ICollection<FaultReport> FaultReports { get; set; } = new List<FaultReport>();
        public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
        public virtual ICollection<NewFridgeRequest> NewFridgeRequests { get; set; } = new List<NewFridgeRequest>();

        // Computed properties with enhanced validation
        [Display(Name = "Total Fridges")]
        public int TotalFridges => Fridges?.Count(f => f.Status != FridgeStatus.Scrapped) ?? 0;

        [Display(Name = "Active Fridges")]
        public int ActiveFridges => Fridges?.Count(f => f.Status != FridgeStatus.Scrapped) ?? 0;

        [Display(Name = "Total Fault Reports")]
        public int TotalFaults => FaultReports?.Count ?? 0;

        [Display(Name = "Active Fault Reports")]
        public int ActiveFaults => FaultReports?.Count(fr =>
            fr.Status == FaultStatus.Reported ||
            fr.Status == FaultStatus.Diagnosed ||
            fr.Status == FaultStatus.Scheduled) ?? 0;

        [Display(Name = "Customer Since")]
        public int CustomerSinceMonths => (int)((DateTime.UtcNow - RegistrationDate).TotalDays / 30);

        [Display(Name = "Fridge Utilization Rate")]
        [Range(0, 100, ErrorMessage = "Utilization rate must be between 0 and 100")]
        public double UtilizationRate => TotalFridges > 0 ? (double)ActiveFridges / TotalFridges * 100 : 0;

        // Business rule validation method
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Type == CustomerType.Other && string.IsNullOrWhiteSpace(OtherBusinessType))
            {
                yield return new ValidationResult(
                    "Custom business type is required when selecting 'Other' business type",
                    new[] { nameof(OtherBusinessType) });
            }

            if (RegistrationDate > DateTime.UtcNow)
            {
                yield return new ValidationResult(
                    "Registration date cannot be in the future",
                    new[] { nameof(RegistrationDate) });
            }
        }
    }
}