using FluentAssertions;
using MockQueryable.NSubstitute;
using NSubstitute;
using Orders.Application.Common.Exceptions;
using Orders.Application.Common.Interfaces;
using Orders.Application.Feature.Orders.OrderList;
using Orders.Domain.Models;
using Xunit;

namespace Orders.Test.Feature
{
    public class OrderListHandlerTests
    {
        private readonly IOrdersDbContext _dbContext = Substitute.For<IOrdersDbContext>();

        [Fact]
        public async Task Handle_WhenOrdersExist_ReturnsPagedResponse()
        {
            // Arrange
            var orders = new List<Order>
            {
                new Order
                {
                    OrderId = Guid.NewGuid(),
                    OrderNumber = "ORD-001",
                    CustomerId = "CUST-001",
                    CustomerName = "John Doe",
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = 150000,
                    StatusCode = "COMPLETED",
                    PaymentStatus = "PAID",
                    IsDeleted = false
                },
                new Order
                {
                    OrderId = Guid.NewGuid(),
                    OrderNumber = "ORD-002",
                    CustomerId = "CUST-002",
                    CustomerName = "Jane Smith",
                    OrderDate = DateTime.UtcNow.AddMinutes(-5),
                    TotalAmount = 250000,
                    StatusCode = "PENDING",
                    PaymentStatus = "UNPAID",
                    IsDeleted = false
                }
            };

            var mockDbSet = orders.BuildMockDbSet();
            _dbContext.Orders.Returns(mockDbSet);

            var handler = new OrderListHandler(_dbContext);
            var query = new OrderListQuery
            {
                Page = 1,
                PageSize = 10
            };

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(2);
            result.Data.Should().HaveCount(2);
            result.Data[0].OrderNumber.Should().Be("ORD-001");
        }

        [Fact]
        public async Task Handle_WhenFilterApplied_ReturnsFilteredOrders()
        {
            // Arrange
            var orders = new List<Order>
            {
                new Order
                {
                    OrderId = Guid.NewGuid(),
                    OrderNumber = "ORD-ABC",
                    CustomerName = "Alice",
                    OrderDate = DateTime.UtcNow,
                    IsDeleted = false
                },
                new Order
                {
                    OrderId = Guid.NewGuid(),
                    OrderNumber = "ORD-XYZ",
                    CustomerName = "Bob",
                    OrderDate = DateTime.UtcNow,
                    IsDeleted = false
                }
            };

            var mockDbSet = orders.BuildMockDbSet();
            _dbContext.Orders.Returns(mockDbSet);

            var handler = new OrderListHandler(_dbContext);
            var query = new OrderListQuery
            {
                Page = 1,
                PageSize = 10,
                FilterValue = "Alice"
            };

            // Act
            var result = await handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(1);
            result.Data.Should().ContainSingle(x => x.CustomerName == "Alice");
        }

        [Fact]
        public async Task Handle_WhenPageExceedsTotalPages_ThrowsNotFoundException()
        {
            // Arrange
            var orders = new List<Order>
            {
                new Order
                {
                    OrderId = Guid.NewGuid(),
                    OrderNumber = "ORD-001",
                    CustomerName = "Alice",
                    OrderDate = DateTime.UtcNow,
                    IsDeleted = false
                }
            };

            var mockDbSet = orders.BuildMockDbSet();
            _dbContext.Orders.Returns(mockDbSet);

            var handler = new OrderListHandler(_dbContext);
            var query = new OrderListQuery
            {
                Page = 5,
                PageSize = 10
            };

            // Act & Assert
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(query, CancellationToken.None));
        }
    }
}
