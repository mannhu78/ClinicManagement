using ClinicManagement.Api.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClinicManagement.Api.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public int PatientId { get; set; }
        public Patient Patient { get; set; }

        public int DoctorId { get; set; }
        public Doctor Doctor { get; set; }

        public string Reason { get; set; }

        public bool Completed { get; set; }
        public bool IsCanceled { get; set; } = false;

        // 🆕 Thêm 2 trường mới:
        [StringLength(500)]
        public string? Diagnosis { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [StringLength(500)]
        public string? CancelReason { get; set; }


    }
}
