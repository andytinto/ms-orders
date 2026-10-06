using System.Collections.Concurrent;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Exceptions;
using Orders.Application.Common.Interfaces;
using Orders.Application.Feature.Orders.Dtos;
using Orders.Domain.Enums;
using Orders.Domain.Models;

namespace Orders.Application.Feature.Orders.CreateOrder
{
    public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderDetailResponseDto>
    {
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _idempotencyLocks = new();
        private readonly IOrdersDbContext _dbContext;

        public CreateOrderHandler(IOrdersDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<OrderDetailResponseDto> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var keyLock = _idempotencyLocks.GetOrAdd(request.IdempotencyKey, _ => new SemaphoreSlim(1, 1));
                await keyLock.WaitAsync(cancellationToken);
                try
                {
                    return await ProcessOrderCreationAsync(request, cancellationToken);
                }
                finally
                {
                    keyLock.Release();
                    if (keyLock.CurrentCount == 1)
                    {
                        _idempotencyLocks.TryRemove(request.IdempotencyKey, out _);
                    }
                }
            }

            return await ProcessOrderCreationAsync(request, cancellationToken);
        }

        private async Task<OrderDetailResponseDto> ProcessOrderCreationAsync(CreateOrderCommand request, CancellationToken cancellationToken)
        {
            // Scenario C: If IdempotencyKey is specified, check if already created
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existingOrder = await _dbContext.Orders
                    .Include(o => o.OrderDetails)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.IdempotencyKey == request.IdempotencyKey, cancellationToken);

                if (existingOrder != null)
                {
                    return MapToResponse(existingOrder);
                }
            }

            // Consistent ordering by ProductId to prevent database deadlock in concurrent transactions
            var sortedItems = request.Items
                .GroupBy(i => i.ProductId)
                .Select(g => new OrderItemRequest
                {
                    ProductId = g.Key,
                    Quantity = g.Sum(x => x.Quantity)
                })
                .OrderBy(i => i.ProductId)
                .ToList();

            var productIds = sortedItems.Select(i => i.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
                .ToListAsync(cancellationToken);

            var productMap = products.ToDictionary(p => p.Id);

            foreach (var item in sortedItems)
            {
                if (!productMap.TryGetValue(item.ProductId, out var product))
                {
                    throw new NotFoundException($"Product with ID '{item.ProductId}' was not found.");
                }

                // Scenario A: Check stock availability
                if (product.StockQuantity < item.Quantity)
                {
                    throw new UnprocessableEntityException(
                        $"Insufficient stock for product '{product.Name}'. Available: {product.StockQuantity}, Requested: {item.Quantity}.");
                }

                // Deduct stock (tracked by EF Core with RowVersion token and SQL Check Constraint)
                product.StockQuantity -= item.Quantity;
            }

            var orderId = Guid.NewGuid();
            var orderNumber = $"ORD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";

            var order = new Order
            {
                OrderId = orderId,
                OrderNumber = orderNumber,
                CustomerId = request.CustomerId,
                CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? request.CustomerId : request.CustomerName,
                ShippingAddress = request.ShippingAddress,
                OrderDate = DateTime.UtcNow,
                StatusCode = OrderStatus.Pending,
                PaymentStatus = "Unpaid",
                IdempotencyKey = request.IdempotencyKey
            };

            decimal totalAmount = 0;
            foreach (var item in sortedItems)
            {
                var product = productMap[item.ProductId];
                var subTotal = product.Price * item.Quantity;
                totalAmount += subTotal;

                order.OrderDetails.Add(new OrderDetail
                {
                    OrderDetailId = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = product.Id.ToString(),
                    ProductName = product.Name,
                    Quantity = item.Quantity,
                    UnitPrice = product.Price,
                    SubTotal = subTotal
                });
            }

            order.TotalAmount = totalAmount;
            _dbContext.Orders.Add(order);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Scenario A concurrency conflict on Product.RowVersion
                throw new ConflictException("Stock level was updated by another transaction concurrently. Please try again.");
            }
            catch (DbUpdateException ex)
            {
                // Scenario C: If unique constraint on IdempotencyKey triggers race condition
                if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
                {
                    var raceOrder = await _dbContext.Orders
                        .Include(o => o.OrderDetails)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.IdempotencyKey == request.IdempotencyKey, cancellationToken);

                    if (raceOrder != null)
                    {
                        return MapToResponse(raceOrder);
                    }
                }

                // Or check constraint violation on CK_Product_StockQuantity_NonNegative
                if (ex.InnerException?.Message.Contains("CK_Product_StockQuantity_NonNegative") == true)
                {
                    throw new UnprocessableEntityException("Stock level cannot be negative. Order rejected.");
                }

                throw;
            }

            return MapToResponse(order);
        }

        private static OrderDetailResponseDto MapToResponse(Order order)
        {
            return new OrderDetailResponseDto
            {
                OrderId = order.OrderId,
                OrderNumber = order.OrderNumber,
                CustomerId = order.CustomerId,
                CustomerName = order.CustomerName,
                ShippingAddress = order.ShippingAddress,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                StatusCode = order.StatusCode,
                PaymentStatus = order.PaymentStatus,
                Notes = order.Notes,
                IdempotencyKey = order.IdempotencyKey,
                RowVersion = order.RowVersion != null ? Convert.ToBase64String(order.RowVersion) : string.Empty,
                Items = order.OrderDetails.Select(d => new OrderItemDetailDto
                {
                    OrderDetailId = d.OrderDetailId,
                    ProductId = d.ProductId,
                    ProductName = d.ProductName,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    SubTotal = d.SubTotal
                }).ToList()
            };
        }
    }
}
