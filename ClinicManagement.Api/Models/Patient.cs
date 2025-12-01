using System.ComponentModel.DataAnnotations;

namespace ClinicManagement.Api.Models
{
    public class Patient
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; }

        public string Email { get; set; } // ✅ Thêm dòng này

        public string PhoneNumber { get; set; }

        public DateTime DateOfBirth { get; set; }

        public string Address { get; set; }
    }
}
