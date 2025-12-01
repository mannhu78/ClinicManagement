namespace ClinicManagement.Api.DTOs
{ 
    public class SubmitDiagnosisRequest
    {
        public int AppointmentId { get; set; }
        public string Diagnosis { get; set; }
        public string Notes { get; set; }
    }
}
