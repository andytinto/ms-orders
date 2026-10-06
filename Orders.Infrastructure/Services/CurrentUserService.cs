using Orders.Application.Common.Interfaces;

namespace Orders.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        public string? UserId { get; set; }
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? EmployeeId { get; set; }
        public string? PhoneNumber { get; set; }
        public double TimeOffSet { get; set; }
        public List<string> RolesInformation { get; set; } = new();
    }
}
