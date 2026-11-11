namespace EmployeeManagementSystem.DTOs.Auth
{
    public class AuthResponseDto
    {

        public string Token { get; set; } = string.Empty;

        public string EmployeeId { get; set; } = string.Empty;

        public string EmployeeCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }
}
