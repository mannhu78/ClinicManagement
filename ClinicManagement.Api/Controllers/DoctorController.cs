using ClinicManagement.Api.Data;
using ClinicManagement.Api.Models;
using ClinicManagement.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClinicManagement.Api.Helpers;
using ClinicManagement.Api.Services;

namespace ClinicManagement.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Doctor")]
    public class DoctorApiController : ControllerBase
    {
        private readonly ClinicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly EmailService _emailService;

        public DoctorApiController(ClinicContext context, UserManager<ApplicationUser> userManager, IWebHostEnvironment env, EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _emailService = emailService;
        }

        // 🩺 Lịch hẹn theo ngày
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAppointments(DateTime? date)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == user.Email);

            if (doctor == null)
                return Unauthorized(new { message = "Tài khoản chưa được gán vào danh sách bác sĩ" });

            var target = date ?? DateTime.Today;

            var data = await _context.Appointments
                .Include(a => a.Patient)
                .Where(a => a.DoctorId == doctor.Id &&
                            a.StartTime.Date == target.Date &&
                            !a.IsCanceled)
                .OrderBy(a => a.StartTime)
                .Select(a => AppointmentToDto(a))
                .ToListAsync();

            return Ok(new { date = target, data });
        }

        // 🧾 Lịch theo tuần
        [HttpGet("week")]
        public async Task<IActionResult> GetWeekAppointments()
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == user.Email);

            var start = DateTime.Now.StartOfWeek(DayOfWeek.Monday);
            var end = start.AddDays(7);

            var data = await _context.Appointments
                .Include(a => a.Patient)
                .Where(a => a.DoctorId == doctor.Id &&
                            a.StartTime >= start &&
                            a.StartTime < end &&
                            !a.IsCanceled)
                .OrderBy(a => a.StartTime)
                .Select(a => AppointmentToDto(a))
                .ToListAsync();

            return Ok(new { start, end, data });
        }

        // 📋 Chi tiết lịch hẹn
        [HttpGet("appointment/{id}")]
        public async Task<IActionResult> GetAppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == id);

            return appointment == null
                ? NotFound(new { message = "Không tìm thấy lịch hẹn" })
                : Ok(AppointmentToDto(appointment));
        }

        // 💬 Gửi kết quả khám
        [HttpPost("diagnosis")]
        public async Task<IActionResult> SubmitDiagnosis([FromBody] SubmitDiagnosisRequest model)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appointment == null)
                return NotFound();

            appointment.Diagnosis = model.Diagnosis;
            appointment.Notes = model.Notes;
            appointment.Completed = true;

            await _context.SaveChangesAsync();

            // 📩 Gửi email kết quả khám cho bệnh nhân
            await _emailService.SendEmailAsync(
                appointment.Patient.Email,
                "Kết quả khám bệnh của bạn",
                $@"
                <h3>Xin chào {appointment.Patient.Name},</h3>
                <p>Bác sĩ <b>{appointment.Doctor.Name}</b> đã hoàn thành kết quả khám bệnh của bạn.</p>
                <p><b>Chẩn đoán:</b> {appointment.Diagnosis}</p>
                <p><b>Ghi chú - Hướng dẫn:</b> {appointment.Notes}</p>
                <p>Thời gian khám: {appointment.StartTime:dd/MM/yyyy HH:mm}</p>
                <br/>
                <p>Chúc bạn mau khỏe, xin cảm ơn bạn đã tin tưởng lựa chọn phòng khám!</p>"
            );

            return Ok(new { message = "Cập nhật kết quả khám & gửi email thành công!" });
        }


        // 🕓 Lịch sử khám
        [HttpGet("history")]
        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == user.Email);

            var data = await _context.Appointments
                .Include(a => a.Patient)
                .Where(a => a.DoctorId == doctor.Id && a.Completed)
                .OrderByDescending(a => a.StartTime)
                .Select(a => AppointmentToDto(a))
                .ToListAsync();

            return Ok(data);
        }

        // 👤 Xem profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == user.Email);
            return Ok(doctor);
        }

        // ✏️ Cập nhật profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateDoctorProfileRequest model)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == user.Email);

            doctor.Name = model.Name;
            doctor.Phone = model.Phone;
            doctor.Specialty = model.Specialty;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Cập nhật hồ sơ thành công" });
        }

        // 📸 Upload avatar
        [HttpPost("upload-avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar([FromForm] UploadAvatarRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Email == user.Email);

            if (request.AvatarFile == null || request.AvatarFile.Length == 0)
                return BadRequest("Vui lòng chọn ảnh.");

            var uploads = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploads)) Directory.CreateDirectory(uploads);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(request.AvatarFile.FileName)}";
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await request.AvatarFile.CopyToAsync(stream);

            doctor.AvatarPath = "/uploads/" + fileName;
            await _context.SaveChangesAsync();

            return Ok(new { avatar = doctor.AvatarPath, message = "Upload thành công" });
        }



        // 🔐 Đổi mật khẩu
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            return result.Succeeded
                ? Ok(new { message = "Đổi mật khẩu thành công" })
                : BadRequest(result.Errors);
        }

        // Mapper
        private static object AppointmentToDto(Appointment a) => new
        {
            a.Id,
            a.StartTime,
            a.Reason,
            a.Diagnosis,
            a.Notes,
            PatientName = a.Patient?.Name,
            Status = a.IsCanceled ? "Canceled" : a.Completed ? "Completed" : "Upcoming"
        };

    }
}
