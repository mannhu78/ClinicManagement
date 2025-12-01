namespace ClinicManagement.Api.DTOs
{
    public class CreateDoctorRequest
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string Phone { get; set; }
        public string Specialty { get; set; }
    }
}
