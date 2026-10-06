using MediatR;
using Microsoft.AspNetCore.Mvc;
using Orders.Application.Common.Models;
using Orders.Application.Feature.Orders.CancelOrder;
using Orders.Application.Feature.Orders.CreateOrder;
using Orders.Application.Feature.Orders.Dtos;
using Orders.Application.Feature.Orders.GetOrderById;
using Orders.Application.Feature.Orders.OrderList;
using Orders.Application.Feature.Orders.UpdateOrderStatus;
using Orders.Domain.Enums;

namespace Orders.Api.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrderController : BaseController
    {
        private readonly ISender _sender;

        public OrderController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>
        /// Create a new order with items, shipping address, and idempotency protection.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<OrderDetailResponseDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> CreateOrder(
            [FromBody] CreateOrderCommand command,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                command.IdempotencyKey = idempotencyKey;
            }

            var result = await _sender.Send(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, ApiResponse<OrderDetailResponseDto>.Ok(result, "Order created successfully."));
        }

        /// <summary>
        /// Get order details by ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApiResponse<OrderDetailResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken cancellationToken)
        {
            var query = new GetOrderByIdQuery { OrderId = id };
            var result = await _sender.Send(query, cancellationToken);
            return OkResponse(ApiResponse<OrderDetailResponseDto>.Ok(result));
        }

        /// <summary>
        /// List orders via GET query parameters (page, pageSize, status, customerId, date range).
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<OrderItemDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetListFromQuery([FromQuery] OrderListQuery query, CancellationToken cancellationToken)
        {
            var response = await _sender.Send(query, cancellationToken);
            return OkResponse(ApiResponse<PagedResponse<OrderItemDto>>.Ok(response));
        }

        /// <summary>
        /// List orders with pagination, status, customer, date range, and text filtering (POST body).
        /// </summary>
        [HttpPost("list")]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<OrderItemDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetList([FromBody] OrderListQuery query, CancellationToken cancellationToken)
        {
            var response = await _sender.Send(query, cancellationToken);
            return OkResponse(ApiResponse<PagedResponse<OrderItemDto>>.Ok(response));
        }

        /// <summary>
        /// Update order status using Finite State Machine and optimistic concurrency control (RowVersion).
        /// </summary>
        [HttpPut("{id:guid}/status")]
        [ProducesResponseType(typeof(ApiResponse<OrderDetailResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> UpdateStatus(
            [FromRoute] Guid id,
            [FromBody] UpdateOrderStatusRequest request,
            CancellationToken cancellationToken)
        {
            var command = new UpdateOrderStatusCommand
            {
                OrderId = id,
                NewStatus = request.NewStatus,
                RowVersion = request.RowVersion
            };

            var result = await _sender.Send(command, cancellationToken);
            return OkResponse(ApiResponse<OrderDetailResponseDto>.Ok(result, "Order status updated successfully."));
        }

        /// <summary>
        /// Cancel an order and automatically restore product inventory.
        /// </summary>
        [HttpPost("{id:guid}/cancel")]
        [ProducesResponseType(typeof(ApiResponse<OrderDetailResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
        public async Task<IActionResult> CancelOrder(
            [FromRoute] Guid id,
            [FromBody] CancelOrderRequest request,
            CancellationToken cancellationToken)
        {
            var command = new CancelOrderCommand
            {
                OrderId = id,
                Reason = request.Reason,
                RowVersion = request.RowVersion
            };

            var result = await _sender.Send(command, cancellationToken);
            return OkResponse(ApiResponse<OrderDetailResponseDto>.Ok(result, "Order cancelled successfully and stock has been restored."));
        }
    }

    public class UpdateOrderStatusRequest
    {
        public string NewStatus { get; set; } = string.Empty;
        public string RowVersion { get; set; } = string.Empty;
    }

    public class CancelOrderRequest
    {
        public string? Reason { get; set; }
        public string RowVersion { get; set; } = string.Empty;
    }
}
