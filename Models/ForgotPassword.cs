using System.ComponentModel.DataAnnotations;

namespace GRP_03_27.Models
{
    public class ForgotPassword
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }

}
