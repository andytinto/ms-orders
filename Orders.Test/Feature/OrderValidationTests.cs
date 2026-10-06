using FluentAssertions;
using Orders.Application.Feature.Orders.CancelOrder;
using Orders.Application.Feature.Orders.CreateOrder;
using Orders.Application.Feature.Orders.Dtos;
using Orders.Application.Feature.Orders.UpdateOrderStatus;
using Orders.Domain.Enums;
using Xunit;

namespace Orders.Test.Feature
{
    public class OrderValidationTests
    {
        private readonly CreateOrderCommandValidator _createValidator = new();
        private readonly UpdateOrderStatusCommandValidator _updateValidator = new();
        private readonly CancelOrderCommandValidator _cancelValidator = new();

        [Fact]
        public async Task CreateOrderValidator_WhenValid_ShouldNotHaveErrors()
        {
            var command = new CreateOrderCommand
            {
                CustomerId = "CUST-001",
                CustomerName = "John Doe",
                ShippingAddress = "Jl. Sudirman No. 123",
                Items = new List<OrderItemRequest>
                {
                    new() { ProductId = Guid.NewGuid(), Quantity = 2 }
                }
            };

            var result = await _createValidator.ValidateAsync(command);
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task CreateOrderValidator_WhenItemsEmpty_ShouldFail()
        {
            var command = new CreateOrderCommand
            {
                CustomerId = "CUST-001",
                ShippingAddress = "Jl. Sudirman No. 123",
                Items = new List<OrderItemRequest>()
            };

            var result = await _createValidator.ValidateAsync(command);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "Items");
        }

        [Fact]
        public async Task CreateOrderValidator_WhenQuantityZeroOrNegative_ShouldFail()
        {
            var command = new CreateOrderCommand
            {
                CustomerId = "CUST-001",
                ShippingAddress = "Jl. Sudirman No. 123",
                Items = new List<OrderItemRequest>
                {
                    new() { ProductId = Guid.NewGuid(), Quantity = 0 }
                }
            };

            var result = await _createValidator.ValidateAsync(command);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Quantity must be greater than zero"));
        }

        [Fact]
        public async Task UpdateOrderStatusValidator_WhenValid_ShouldPass()
        {
            var command = new UpdateOrderStatusCommand
            {
                OrderId = Guid.NewGuid(),
                NewStatus = OrderStatus.Confirmed,
                RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 })
            };

            var result = await _updateValidator.ValidateAsync(command);
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task UpdateOrderStatusValidator_WhenInvalidStatus_ShouldFail()
        {
            var command = new UpdateOrderStatusCommand
            {
                OrderId = Guid.NewGuid(),
                NewStatus = "UnknownStatus",
                RowVersion = "AQIDBA=="
            };

            var result = await _updateValidator.ValidateAsync(command);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "NewStatus");
        }

        [Fact]
        public async Task CancelOrderValidator_WhenRowVersionMissing_ShouldFail()
        {
            var command = new CancelOrderCommand
            {
                OrderId = Guid.NewGuid(),
                Reason = "Wrong item",
                RowVersion = string.Empty
            };

            var result = await _cancelValidator.ValidateAsync(command);
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.PropertyName == "RowVersion");
        }
    }
}
