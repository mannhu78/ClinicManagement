using System.ComponentModel.DataAnnotations;

namespace ClinicManagement.Api.Models
{
    public class Doctor
    {
        public int Id { get; set; }

        [Required, StringLength(120)]
        public string Name { get; set; }

        [Required, EmailAddress, StringLength(100)]
        public string Email { get; set; }   // ✅ thêm email để tạo tài khoản đăng nhập

        [StringLength(100)]
        public string Specialty { get; set; }

        [StringLength(50)]
        public string Phone { get; set; }
        // 🖼 Ảnh đại diện
        [StringLength(255)]
        public string? AvatarPath { get; set; }
    }
}
