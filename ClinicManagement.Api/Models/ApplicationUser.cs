using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ClinicManagement.Api.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required, StringLength(150)]
        public string FullName { get; set; }

        [StringLength(100)]
        public string Role { get; set; }  // Admin / Doctor / User
    }
}
