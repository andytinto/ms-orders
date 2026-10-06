using Microsoft.AspNetCore.Mvc;

namespace Orders.Api.Controllers
{
    [ApiController]
    [Route("api")]
    public class BaseController : Controller
    {
        [NonAction]
        public OkObjectResult OkResponse(object value)
        {
            return new OkObjectResult(value);
        }
    }
}
