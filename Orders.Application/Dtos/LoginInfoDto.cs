namespace Orders.Application.Dtos
{
    public class LoginInfoDto
    {
        public string? UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? EmployeeId { get; set; }
        public string? PhoneNumber { get; set; }
        public List<string> RolesInformation { get; set; } = new();
    }
}
