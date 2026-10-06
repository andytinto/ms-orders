using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Interfaces;
using Orders.Application.Common.Models;
using Orders.Domain.Models;

namespace Orders.Api.Controllers
{
    [ApiController]
    [Route("api/products")]
    public class ProductController : BaseController
    {
        private readonly IOrdersDbContext _dbContext;

        public ProductController(IOrdersDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
        {
            var products = await _dbContext.Products
                .AsNoTracking()
                .OrderBy(p => p.Name)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.StockQuantity,
                    p.Price,
                    RowVersion = Convert.ToBase64String(p.RowVersion ?? Array.Empty<byte>())
                })
                .ToListAsync(cancellationToken);

            return OkResponse(ApiResponse<object>.Ok(products));
        }

        [HttpPost]
        public async Task<IActionResult> CreateOrSeed([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
        {
            var product = new Product
            {
                Id = request.Id ?? Guid.NewGuid(),
                Name = request.Name,
                StockQuantity = request.StockQuantity,
                Price = request.Price,
                RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks)
            };

            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return OkResponse(ApiResponse<object>.Ok(new
            {
                product.Id,
                product.Name,
                product.StockQuantity,
                product.Price
            }, "Product created successfully."));
        }
    }

    public class CreateProductRequest
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int StockQuantity { get; set; }
        public decimal Price { get; set; }
    }
}
