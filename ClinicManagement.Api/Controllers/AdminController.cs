using ClinicManagement.Api.Data;
using ClinicManagement.Api.Models;
using ClinicManagement.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicManagement.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminApiController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ClinicContext _context;

        public AdminApiController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ClinicContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        // 📍 Lấy danh sách user và role
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserDto
                {
                    Id = user.Id,
                    FullName = user.FullName,
                    Email = user.Email,
                    Role = roles.FirstOrDefault() ?? "User"
                });
            }

            return Ok(userList);
        }
        [HttpPost("create-doctor")]
        public async Task<IActionResult> CreateDoctor([FromBody] CreateDoctorRequest model)
        {
            // Email tồn tại chưa?
            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
                return BadRequest(new { message = "Email đã tồn tại!" });

            // Tạo user
            var user = new ApplicationUser
            {
                FullName = model.FullName,
                Email = model.Email,
                UserName = model.Email,
                PhoneNumber = model.Phone,
                Role = "Doctor"
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            // Gán role Doctor
            await _userManager.AddToRoleAsync(user, "Doctor");

            // Tạo hồ sơ Doctor
            _context.Doctors.Add(new Doctor
            {
                Name = model.FullName,
                Email = model.Email,
                Phone = model.Phone,
                Specialty = model.Specialty,
                AvatarPath = "/images/default-doctor.png"
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Tạo tài khoản bác sĩ thành công!" });
        }


        // 🔁 Thay đổi role user
        [HttpPut("change-role")]
        public async Task<IActionResult> ChangeRole(ChangeRoleRequest request)
        {
            var user = await _userManager.FindByIdAsync(request.UserId);
            if (user == null) return NotFound(new { message = "Không tìm thấy user" });

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, request.Role);

            user.Role = request.Role;
            await _userManager.UpdateAsync(user);

            // Tạo hồ sơ bác sĩ nếu gán role Doctor
            if (request.Role == "Doctor")
            {
                var exDoctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == user.Email);
                if (exDoctor == null)
                {
                    _context.Doctors.Add(new Doctor
                    {
                        Name = user.FullName ?? "Chưa cập nhật",
                        Email = user.Email,
                        Phone = user.PhoneNumber ?? "N/A",
                        Specialty = "Chưa cập nhật",
                        AvatarPath = "/images/default-doctor.png"
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return Ok(new { message = "Cập nhật role thành công" });
        }

        // ❌ Xoá user
        [HttpDelete("user/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.DeleteAsync(user);
            return Ok(new { message = "Xoá user thành công" });
        }

        // 📊 Thống kê hệ thống
        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var totalAppointments = _context.Appointments.Count();
            var completed = _context.Appointments.Count(a => a.Completed);
            var rate = totalAppointments > 0 ? (double)completed / totalAppointments * 100 : 0;

            var byDoctor = _context.Appointments
                .Include(a => a.Doctor)
                .AsEnumerable()
                .GroupBy(a => a.Doctor?.Name ?? "Unknown")
                .Select(g => new { Doctor = g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count);

            var byDate = _context.Appointments
                .AsEnumerable()
                .GroupBy(a => a.StartTime.Date)
                .Select(g => new { Date = g.Key.ToString("yyyy-MM-dd"), Count = g.Count() });

            return Ok(new
            {
                completionRate = Math.Round(rate, 1),
                appointmentsByDoctor = byDoctor,
                appointmentsByDate = byDate
            });
        }
    }
}
