using ClinicManagement.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Numerics;

namespace ClinicManagement.Api.Data
{
    public class ClinicContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
    {
        public ClinicContext(DbContextOptions<ClinicContext> options) : base(options) { }

        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Mapping đơn giản – bạn chỉnh lại cho khớp với clinic_db.sql nếu cần
            builder.Entity<Doctor>().ToTable("doctors");
            builder.Entity<Patient>().ToTable("patients");
            builder.Entity<Appointment>().ToTable("appointments");
            builder.Entity<RefreshToken>().ToTable("refreshtokens");
        }
    }
}
