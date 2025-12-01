
using ClinicManagement.Api.Services;
using ClinicManagement.Api.Data;
using ClinicManagement.Api.DTOs;
using ClinicManagement.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Numerics;
using System.Net.NetworkInformation;

namespace ClinicApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "User")]
    public class UserApiController : ControllerBase
    {
        private readonly ClinicContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;

        public UserApiController(ClinicContext context, UserManager<ApplicationUser> userManager, EmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        // 🧭 1) Dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var doctors = await _context.Doctors.ToListAsync();

            var facilities = new List<object>
            {
                new { Image = "/images/facility1.jpg", Title = "Phòng khám hiện đại" },
                new { Image = "/images/facility2.jpg", Title = "Thiết bị y tế tiên tiến" },
                new { Image = "/images/facility3.jpg", Title = "Khu vực chờ tiện nghi" }
            };

            return Ok(new { doctors, facilities });
        }

        // 👤 2) Xem profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            return Ok(new
            {
                user.FullName,
                user.Email,
                user.PhoneNumber,
                user.UserName
            });
        }

        // ✏️ 3) Cập nhật profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile(ProfileDto model)
        {
            var user = await _userManager.GetUserAsync(User);

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;

            await _userManager.UpdateAsync(user);
            return Ok(new { message = "Cập nhật thông tin thành công!" });
        }

        // 🧾 4) Danh sách lịch hẹn
        [HttpGet("appointments")]
        public async Task<IActionResult> GetAppointments()
        {
            var user = await _userManager.GetUserAsync(User);

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == user.Email);
            if (patient == null)
                return Ok(new { success = false, message = "Chưa có hồ sơ bệnh nhân." });

            var appointments = await _context.Appointments
                .Include(a => a.Doctor)
                .Where(a => a.PatientId == patient.Id)
                .OrderByDescending(a => a.StartTime)
                .ToListAsync();

            var result = appointments.Select(a => ToAppointmentResponse(a)).ToList();

            return Ok(new { success = true, data = result });
        }


        // 📋 5) Chi tiết lịch hẹn
        [HttpGet("appointments/{id}")]
        public async Task<IActionResult> GetAppointmentDetails(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == id);

            return appointment == null ? NotFound() : Ok(appointment);
        }

        // 📅 6) Đặt lịch hẹn
        [HttpPost("book")]
        public async Task<IActionResult> Book([FromBody] BookAppointmentRequest model)
        {
            var user = await _userManager.GetUserAsync(User);

            // Convert chuỗi giờ nhập sang DateTime
            if (!DateTime.TryParseExact(model.StartTime,
                                       "dd/MM/yyyy HH:mm",
                                       System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None,
                                       out DateTime startTime))
            {
                return BadRequest(new { message = "Sai định dạng ngày giờ. Vui lòng nhập theo format: dd/MM/yyyy HH:mm" });
            }

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == user.Email);
            if (patient == null)
            {
                patient = new Patient
                {
                    Name = user.FullName,
                    Email = user.Email,
                    Address = "Chưa cập nhật",
                    PhoneNumber = "N/A"
                };
                _context.Patients.Add(patient);
                await _context.SaveChangesAsync();
            }

            bool isConflict = await _context.Appointments.AnyAsync(a =>
                a.DoctorId == model.DoctorId &&
                !a.IsCanceled &&
                EF.Functions.DateDiffMinute(a.StartTime, startTime) == 0);

            if (isConflict)
                return BadRequest(new { message = "Khung giờ đã có người đặt." });

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = model.DoctorId,
                StartTime = startTime,
                Reason = model.Reason,
                Completed = false,
                IsCanceled = false
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == model.DoctorId);

            await _emailService.SendEmailAsync(
                user.Email,
                "Xác nhận đặt lịch khám",
                $@"
                <h3>Xin chào {user.FullName},</h3>
                <p>Bạn đã đặt lịch với <b>Bác sĩ {doctor.Name}</b> ({doctor.Specialty})</p>
                <p>Thời gian: <b>{startTime:dd/MM/yyyy HH:mm}</b></p>
                <p>Lý do :<b> {model.Reason}</b></p>
                <br/>
                <p>Cảm ơn bạn đã tin tưởng và lựa chọn phòng khám.</p>
                <br/>
                <p><i>(Đây là email tự động, vui lòng không phản hồi)</i></p>"
            );

            return Ok(new { message = "Đặt lịch thành công!" });
        }


        // ⏱ 7) Khung giờ trống
        [HttpGet("available-slots")]
        public async Task<IActionResult> GetAvailableSlots(int doctorId, DateTime date)
        {
            var startOfDay = date.Date.AddHours(8);
            var endOfDay = date.Date.AddHours(17);
            var duration = TimeSpan.FromMinutes(30);

            var booked = await _context.Appointments
                .Where(a => a.DoctorId == doctorId && a.StartTime.Date == date.Date && !a.IsCanceled)
                .Select(a => a.StartTime)
                .ToListAsync();

            var slots = new List<string>();
            for (var t = startOfDay; t < endOfDay; t += duration)
                if (!booked.Any(a => a >= t && a < t + duration))
                    slots.Add(t.ToString("HH:mm"));

            return Ok(slots);
        }

        // ❌ 8) Hủy lịch hẹn
        [HttpPost("cancel")]
        public async Task<IActionResult> CancelAppointment([FromBody] CancelAppointmentRequest model)
        {
            var user = await _userManager.GetUserAsync(User);

            var appointment = await _context.Appointments
                .Include(a => a.Doctor)
                .Include(a => a.Patient)
                .FirstOrDefaultAsync(a => a.Id == model.AppointmentId);

            if (appointment == null)
                return NotFound(new { message = "Không tìm thấy lịch hẹn." });

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Email == user.Email);
            if (patient == null || appointment.PatientId != patient.Id)
                return Forbid();

            if ((appointment.StartTime - DateTime.Now).TotalHours < 2)
                return BadRequest(new { message = "Không thể hủy lịch trước dưới 2 tiếng." });

            appointment.IsCanceled = true;
            appointment.CancelReason = string.IsNullOrWhiteSpace(model.Reason)
                                        ? "Không có lý do"
                                        : model.Reason;

            _context.Appointments.Update(appointment);
            await _context.SaveChangesAsync();

            // Send email
            await _emailService.SendEmailAsync(
                user.Email,
                "Xác nhận hủy lịch khám",
                $@"
                <h3>Xin chào {user.FullName}</h3>
                <p>Bạn đã hủy lịch với bác sĩ <b>{appointment.Doctor.Name}</b>({appointment.Doctor.Specialty})</p>
                <p>Thời gian: <b>{appointment.StartTime:dd/MM/yyyy HH:mm}</b></p>
                <p> <b>Lý do:</b> {appointment.CancelReason}</b> </p>
                <br/>
                <p> Nếu có gì thắc mắc xin hãy liên hệ chúng tôi. Cảm ơn bạn đã tin tưởng và lựa chọn phòng khám ! </p> "
            );

            return Ok(new { message = "Hủy lịch thành công!" });
        }
        private AppointmentResponse ToAppointmentResponse(Appointment a)
        {
            return new AppointmentResponse
            {
                Id = a.Id,
                DoctorName = a.Doctor?.Name ?? "",
                Specialty = a.Doctor?.Specialty ?? "",
                DoctorAvatar = a.Doctor?.AvatarPath ?? "",
                StartTime = a.StartTime,
                Reason = a.Reason,
                Status = a.IsCanceled ? "Canceled" :
                         a.Completed ? "Completed" : "Upcoming",
                CancelReason = a.CancelReason
            };
        }
        // 🔐 9) Thay đổi mật khẩu
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { message = "Người dùng không tồn tại" });

            var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                return BadRequest(new { message = "Thay đổi mật khẩu thất bại", errors });
            }

            // Gửi email thông báo
            await _emailService.SendEmailAsync(
                user.Email,
                "Thay đổi mật khẩu thành công",
                $@"
                <h3>Xin chào {user.FullName},</h3>
                <p>Mật khẩu tài khoản của bạn đã được thay đổi thành công.</p>
                <p>Nếu bạn không thực hiện thao tác này, vui lòng liên hệ đội ngũ hỗ trợ ngay lập tức.</p>
                <br/>
                <p>Cảm ơn bạn đã sử dụng dịch vụ của chúng tôi.</p>
                <p><i>(Đây là email tự động, vui lòng không trả lời)</i></p>"
            );

            return Ok(new { message = "Thay đổi mật khẩu thành công!" });
        }


    }

}
