using Microsoft.AspNetCore.Mvc;
using Orders.Application.Interfaces;

namespace Orders.Api.Controllers
{
    [Route("api/[controller]")]
    public class HealthController : Controller
    {
        private readonly IAppsettingServices _appsettingServices;

        public HealthController(IAppsettingServices appsettingServices)
        {
            _appsettingServices = appsettingServices;
        }

        // GET: api/health
        [HttpGet]
        public ResultMessage Get()
        {
            return new ResultMessage()
            {
                Message = $"Connected {_appsettingServices.GetBasicOptions().ServiceName} {_appsettingServices.GetBasicOptions().Environment}"
            };
        }

        public class ResultMessage
        {
            public string Message { get; set; } = string.Empty;
        }
    }
}
