using System.ComponentModel.DataAnnotations;
using GRP_03_27.Enums;

namespace GRP_03_27.Models.ViewModels
{
    public class CustomerProfileViewModel
    {
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "Business name is required")]
        [StringLength(200, ErrorMessage = "Business name cannot exceed 200 characters")]
        [Display(Name = "Business Name")]
        public string BusinessName { get; set; }

        [Required(ErrorMessage = "Contact person is required")]
        [StringLength(100, ErrorMessage = "Contact person name cannot exceed 100 characters")]
        [Display(Name = "Contact Person")]
        public string ContactPerson { get; set; }

        [Required(ErrorMessage = "Email address is required")]
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [Display(Name = "Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        [Display(Name = "Phone Number")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Physical address is required")]
        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        [Display(Name = "Physical Address")]
        public string PhysicalAddress { get; set; }

        [Required(ErrorMessage = "Business type is required")]
        [Display(Name = "Business Type")]
        public CustomerType BusinessType { get; set; }

        [Display(Name = "Specify Other Business Type")]
        [StringLength(100, ErrorMessage = "Business type cannot exceed 100 characters")]
        public string? OtherBusinessType { get; set; }
    }
}