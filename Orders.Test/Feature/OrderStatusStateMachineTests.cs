using FluentAssertions;
using Orders.Domain.Enums;
using Xunit;

namespace Orders.Test.Feature
{
    public class OrderStatusStateMachineTests
    {
        [Theory]
        [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
        [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, true)]
        [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped, true)]
        [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, true)]
        [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
        public void CanTransition_ValidTransitions_ShouldReturnTrue(string current, string next, bool expected)
        {
            var result = OrderStatus.CanTransition(current, next);
            result.Should().Be(expected);
        }

        [Theory]
        [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
        [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
        [InlineData(OrderStatus.Confirmed, OrderStatus.Pending)]
        [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered)]
        [InlineData(OrderStatus.Shipped, OrderStatus.Pending)]
        [InlineData(OrderStatus.Shipped, OrderStatus.Confirmed)]
        [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Delivered, OrderStatus.Pending)]
        [InlineData(OrderStatus.Delivered, OrderStatus.Confirmed)]
        [InlineData(OrderStatus.Delivered, OrderStatus.Shipped)]
        [InlineData(OrderStatus.Delivered, OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
        [InlineData(OrderStatus.Cancelled, OrderStatus.Confirmed)]
        [InlineData(OrderStatus.Cancelled, OrderStatus.Shipped)]
        [InlineData(OrderStatus.Cancelled, OrderStatus.Delivered)]
        public void CanTransition_InvalidTransitions_ShouldReturnFalse(string current, string next)
        {
            var result = OrderStatus.CanTransition(current, next);
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(OrderStatus.Delivered, true)]
        [InlineData(OrderStatus.Cancelled, true)]
        [InlineData(OrderStatus.Pending, false)]
        [InlineData(OrderStatus.Confirmed, false)]
        [InlineData(OrderStatus.Shipped, false)]
        public void IsTerminal_ShouldIdentifyTerminalStatesCorrectly(string status, bool isTerminal)
        {
            var result = OrderStatus.IsTerminal(status);
            result.Should().Be(isTerminal);
        }

        [Fact]
        public void AllStatuses_ShouldContainAllDefinedStatuses()
        {
            OrderStatus.AllStatuses.Should().BeEquivalentTo(new[]
            {
                OrderStatus.Pending,
                OrderStatus.Confirmed,
                OrderStatus.Shipped,
                OrderStatus.Delivered,
                OrderStatus.Cancelled
            });
        }
    }
}
