namespace Orders.Application.Common.Interfaces
{
    public interface ICurrentUserService
    {
        string? UserId { get; set; }
        string? FullName { get; set; }
        string? Email { get; set; }
        string? EmployeeId { get; set; }
        string? PhoneNumber { get; set; }
        double TimeOffSet { get; set; }
        List<string> RolesInformation { get; set; }
    }

    public class LoginInfo
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public List<string> RolesInformation { get; set; } = new();
    }
}
