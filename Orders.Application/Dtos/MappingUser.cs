using System;
using System.Collections.Generic;
using System.Text;

namespace Orders.Application.Dtos
{
    public class MappingUser
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public Guid RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public string EmployeeId { get; set; } = string.Empty;
    }
}
