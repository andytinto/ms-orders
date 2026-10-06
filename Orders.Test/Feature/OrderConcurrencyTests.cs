using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orders.Application.Common.Exceptions;
using Orders.Application.Feature.Orders.CancelOrder;
using Orders.Application.Feature.Orders.CreateOrder;
using Orders.Application.Feature.Orders.Dtos;
using Orders.Application.Feature.Orders.UpdateOrderStatus;
using Orders.Domain.Enums;
using Orders.Domain.Models;
using Orders.Persistance.DbContext;
using Xunit;

namespace Orders.Test.Feature
{
    public class OrderConcurrencyTests
    {
        private OrdersContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<OrdersContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new OrdersContext(options);
        }

        [Fact]
        public async Task SkenarioA_ConcurrentStockDeduction_TwoConcurrentOrders_OnlyOneSucceeds_StockNeverNegative()
        {
            // Arrange
            var dbName = "SkenarioA_StockDeduction_" + Guid.NewGuid();
            var productId = Guid.NewGuid();

            // Initial stock is 15
            using (var seedContext = CreateContext(dbName))
            {
                seedContext.Products.Add(new Product
                {
                    Id = productId,
                    Name = "Product X (Distribution Item)",
                    StockQuantity = 15,
                    Price = 50000m,
                    RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks)
                });
                await seedContext.SaveChangesAsync();
            }

            // Two concurrent users submit order for 10 units simultaneously
            // Total requested: 20 units. Available: 15 units.
            var command1 = new CreateOrderCommand
            {
                CustomerId = "CUST-A",
                CustomerName = "Customer A",
                ShippingAddress = "Jakarta",
                Items = new List<OrderItemRequest> { new() { ProductId = productId, Quantity = 10 } }
            };

            var command2 = new CreateOrderCommand
            {
                CustomerId = "CUST-B",
                CustomerName = "Customer B",
                ShippingAddress = "Surabaya",
                Items = new List<OrderItemRequest> { new() { ProductId = productId, Quantity = 10 } }
            };

            var successCount = 0;
            var failureCount = 0;
            var exceptions = new List<Exception>();

            var barrier = new Barrier(2);

            Task ExecuteOrderAsync(CreateOrderCommand cmd)
            {
                return Task.Run(async () =>
                {
                    using var context = CreateContext(dbName);
                    var handler = new CreateOrderHandler(context);

                    // Synchronize start so both threads enter at the exact same moment
                    barrier.SignalAndWait();

                    try
                    {
                        var result = await handler.Handle(cmd, CancellationToken.None);
                        if (result != null)
                        {
                            Interlocked.Increment(ref successCount);
                        }
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failureCount);
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                });
            }

            // Act: Run both orders concurrently
            await Task.WhenAll(ExecuteOrderAsync(command1), ExecuteOrderAsync(command2));

            // Assert
            successCount.Should().Be(1, "Exactly one order should succeed because available stock is 15 and each requested 10");
            failureCount.Should().Be(1, "The second concurrent order must fail");
            exceptions.Should().ContainSingle(ex =>
                ex is UnprocessableEntityException || ex is ConflictException);

            // Verify final stock in database is exactly 5 (never negative)
            using (var verifyContext = CreateContext(dbName))
            {
                var product = await verifyContext.Products.FindAsync(productId);
                product.Should().NotBeNull();
                product!.StockQuantity.Should().Be(5);
                product.StockQuantity.Should().BeGreaterThanOrEqualTo(0);
            }
        }

        [Fact]
        public async Task SkenarioB_ConcurrentStatusUpdate_ConflictingUpdates_SecondAdminRejectedWithConflictException()
        {
            // Arrange
            var dbName = "SkenarioB_StatusUpdate_" + Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            string initialRowVersionBase64;

            using (var seedContext = CreateContext(dbName))
            {
                seedContext.Products.Add(new Product
                {
                    Id = productId,
                    Name = "Test Product",
                    StockQuantity = 10,
                    Price = 10000m
                });

                var order = new Order
                {
                    OrderId = orderId,
                    OrderNumber = "ORD-TEST-001",
                    CustomerId = "CUST-001",
                    CustomerName = "Admin Target Order",
                    StatusCode = OrderStatus.Confirmed,
                    RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
                    OrderDetails = new List<OrderDetail>
                    {
                        new()
                        {
                            OrderDetailId = Guid.NewGuid(),
                            OrderId = orderId,
                            ProductId = productId.ToString(),
                            ProductName = "Test Product",
                            Quantity = 2,
                            UnitPrice = 10000m,
                            SubTotal = 20000m
                        }
                    }
                };
                seedContext.Orders.Add(order);
                await seedContext.SaveChangesAsync();
                initialRowVersionBase64 = Convert.ToBase64String(order.RowVersion);
            }

            // Admin 1 and Admin 2 both fetched order with initialRowVersionBase64
            // Admin 1 sets status to Shipped
            using (var admin1Context = CreateContext(dbName))
            {
                var admin1Handler = new UpdateOrderStatusHandler(admin1Context);
                var updateResult = await admin1Handler.Handle(new UpdateOrderStatusCommand
                {
                    OrderId = orderId,
                    NewStatus = OrderStatus.Shipped,
                    RowVersion = initialRowVersionBase64
                }, CancellationToken.None);

                updateResult.StatusCode.Should().Be(OrderStatus.Shipped);
            }

            // Admin 2 now tries to cancel the order using the old/stale RowVersion
            using (var admin2Context = CreateContext(dbName))
            {
                var admin2Handler = new UpdateOrderStatusHandler(admin2Context);

                var act = async () => await admin2Handler.Handle(new UpdateOrderStatusCommand
                {
                    OrderId = orderId,
                    NewStatus = OrderStatus.Cancelled,
                    RowVersion = initialRowVersionBase64
                }, CancellationToken.None);

                // Assert: Admin 2 must be rejected with 409 Conflict
                await act.Should().ThrowAsync<ConflictException>()
                    .WithMessage("*Concurrency conflict*");
            }

            // Verify order in database remains Shipped and stock was not mistakenly refunded
            using (var verifyContext = CreateContext(dbName))
            {
                var order = await verifyContext.Orders.FindAsync(orderId);
                order.Should().NotBeNull();
                order!.StatusCode.Should().Be(OrderStatus.Shipped);

                var product = await verifyContext.Products.FindAsync(productId);
                product!.StockQuantity.Should().Be(10, "Stock should not be refunded because cancel was rejected");
            }
        }

        [Fact]
        public async Task SkenarioC_IdempotentCreateUnderRace_TwoParallelRequests_CreatesOnlyOneOrder()
        {
            // Arrange
            var dbName = "SkenarioC_Idempotency_" + Guid.NewGuid();
            var productId = Guid.NewGuid();
            var idempotencyKey = "IDEM-KEY-" + Guid.NewGuid();

            using (var seedContext = CreateContext(dbName))
            {
                seedContext.Products.Add(new Product
                {
                    Id = productId,
                    Name = "Idempotent Product",
                    StockQuantity = 50,
                    Price = 20000m
                });
                await seedContext.SaveChangesAsync();
            }

            var command = new CreateOrderCommand
            {
                CustomerId = "CUST-IDEM",
                CustomerName = "Idempotent Customer",
                ShippingAddress = "Jl. Thamrin No. 1",
                IdempotencyKey = idempotencyKey,
                Items = new List<OrderItemRequest>
                {
                    new() { ProductId = productId, Quantity = 2 }
                }
            };

            var responses = new List<OrderDetailResponseDto>();
            var barrier = new Barrier(2);

            Task ExecuteAsync()
            {
                return Task.Run(async () =>
                {
                    using var context = CreateContext(dbName);
                    var handler = new CreateOrderHandler(context);

                    barrier.SignalAndWait();

                    try
                    {
                        var result = await handler.Handle(command, CancellationToken.None);
                        lock (responses)
                        {
                            responses.Add(result);
                        }
                    }
                    catch (Exception)
                    {
                        // In case of unique constraint race
                    }
                });
            }

            // Act: Run 2 identical requests concurrently
            await Task.WhenAll(ExecuteAsync(), ExecuteAsync());

            // Assert
            using (var verifyContext = CreateContext(dbName))
            {
                var orderCount = await verifyContext.Orders
                    .CountAsync(o => o.IdempotencyKey == idempotencyKey);

                orderCount.Should().Be(1, "Exactly one order should exist for the given idempotency key");

                var product = await verifyContext.Products.FindAsync(productId);
                product!.StockQuantity.Should().Be(48, "Stock should only be deducted once for the single order created");
            }
        }

        [Fact]
        public async Task ExtraRaceCondition_CancelOrder_WhenAlreadyShipped_ThrowsUnprocessableEntityException()
        {
            // Arrange: Order is already in Shipped state
            var dbName = "ExtraRace_CancelShipped_" + Guid.NewGuid();
            var orderId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            string rowVersionBase64;

            using (var seedContext = CreateContext(dbName))
            {
                seedContext.Products.Add(new Product
                {
                    Id = productId,
                    Name = "Extra Race Product",
                    StockQuantity = 10,
                    Price = 15000m
                });

                var order = new Order
                {
                    OrderId = orderId,
                    OrderNumber = "ORD-RACE-001",
                    CustomerId = "CUST-RACE",
                    StatusCode = OrderStatus.Shipped, // Already shipped!
                    RowVersion = BitConverter.GetBytes(DateTime.UtcNow.Ticks),
                    OrderDetails = new List<OrderDetail>
                    {
                        new()
                        {
                            OrderDetailId = Guid.NewGuid(),
                            OrderId = orderId,
                            ProductId = productId.ToString(),
                            ProductName = "Extra Race Product",
                            Quantity = 3,
                            UnitPrice = 15000m,
                            SubTotal = 45000m
                        }
                    }
                };
                seedContext.Orders.Add(order);
                await seedContext.SaveChangesAsync();
                rowVersionBase64 = Convert.ToBase64String(order.RowVersion);
            }

            // Act: Attempt to cancel shipped order
            using (var context = CreateContext(dbName))
            {
                var cancelHandler = new CancelOrderHandler(context);

                var act = async () => await cancelHandler.Handle(new CancelOrderCommand
                {
                    OrderId = orderId,
                    Reason = "Customer wants to cancel",
                    RowVersion = rowVersionBase64
                }, CancellationToken.None);

                // Assert: Must be rejected with UnprocessableEntityException
                await act.Should().ThrowAsync<UnprocessableEntityException>()
                    .WithMessage("*cannot be cancelled*");
            }

            // Verify stock is untouched
            using (var verifyContext = CreateContext(dbName))
            {
                var product = await verifyContext.Products.FindAsync(productId);
                product!.StockQuantity.Should().Be(10, "Stock must NOT be refunded for shipped orders");
            }
        }
    }
}
