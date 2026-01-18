using System.ComponentModel.DataAnnotations;

namespace GRP_03_27.Models
{
    public class Location
    {
        [Key]
        public int LocationId { get; set; }

        [Required]
        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(100)]
        public string GPSCoordinates { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Optional foreign key to customer
        public int? CustomerId { get; set; }
        public virtual Customer? Customer { get; set; }
    }
}
