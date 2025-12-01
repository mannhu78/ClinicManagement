namespace ClinicManagement.Api.DTOs
{
    public class AppointmentResponse
    {
        public int Id { get; set; }
        public string DoctorName { get; set; }
        public string Specialty { get; set; }
        public string DoctorAvatar { get; set; }

        public DateTime StartTime { get; set; }
        public string Reason { get; set; }

        public string Status { get; set; }   // Upcoming / Completed / Canceled

        public string? CancelReason { get; set; }
    }
}
