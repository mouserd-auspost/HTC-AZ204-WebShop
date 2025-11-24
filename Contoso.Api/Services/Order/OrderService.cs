using AutoMapper;
using Contoso.Api.Data;
using Contoso.Api.Models;
using Contoso.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Contoso.Api.Services;

public class OrderService : IOrderService
{
    private readonly ContosoDbContext _context;
    private readonly IMapper _mapper;

    public OrderService(ContosoDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }
    public async Task<IEnumerable<OrderDto>> GetOrdersAsync(int userId)
    {
         // Query orders by partition key (UserId) where possible
         var orders = await _context.Orders
                                    .Where(o => o.UserId == userId)
                                    .ToListAsync();

        // Load items and product data for each order and merge
        foreach (var order in orders)
        {
            order.Items = await _context.OrderItems
                                        .Where(oi => oi.OrderId == order.Id)
                                        .ToListAsync();

            if (order.Items != null)
            {
                foreach (var oi in order.Items)
                {
                    oi.Product = await _context.Products
                                               .Where(p => p.Id == oi.ProductId)
                                               .FirstOrDefaultAsync();
                }
            }
        }

        return _mapper.Map<IEnumerable<OrderDto>>(orders);
    }

    public async Task<OrderDto> GetOrderByIdAsync(int id, int userId)
    {
        var order = await _context.Orders
                            .Where(o => o.UserId == userId && o.Id == id)
                            .FirstOrDefaultAsync();

        if (order == null)
            return null;

        order.Items = await _context.OrderItems
                                    .Where(oi => oi.OrderId == order.Id)
                                    .ToListAsync();

        if (order.Items != null)
        {
            foreach (var oi in order.Items)
            {
                oi.Product = await _context.Products
                                           .Where(p => p.Id == oi.ProductId)
                                           .FirstOrDefaultAsync();
            }
        }

        return _mapper.Map<OrderDto>(order);
    }

    public async Task<OrderDto> CreateOrderAsync(OrderDto orderDto)
    {
        var newOrder = new Order
        {
            UserId = orderDto.UserId,
            Total = orderDto.Total,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Items = new List<OrderItem>()
        };

        // Ensure we assign a non-zero integer Id for Cosmos (simple auto-increment)
        // NOTE: This is a basic implementation and can have race conditions under high concurrency.
        newOrder.Id = await GetNextOrderIdAsync();

        await _context.Orders.AddAsync(newOrder);
        await _context.SaveChangesAsync();

        if (orderDto.Items != null)
        {
            var createdItems = new List<OrderItem>();

            // Get current max OrderItem.Id once to avoid duplicate key tracking
            var maxItemId = await _context.OrderItems.Select(oi => (int?)oi.Id).MaxAsync();
            var nextItemId = (maxItemId ?? 0) + 1;

            foreach (var orderItem in orderDto.Items)
            {
                var newOrderItem = new OrderItem
                {
                    Id = nextItemId++,
                    OrderId = newOrder.Id,
                    ProductId = orderItem.ProductId,
                    Quantity = orderItem.Quantity,
                    UnitPrice = orderItem.Price
                };

                await _context.OrderItems.AddAsync(newOrderItem);
                createdItems.Add(newOrderItem);
            }

            await _context.SaveChangesAsync();

            // Attach items to order object for return
            newOrder.Items = createdItems;
            // Populate product info for returned DTO
            foreach (var oi in newOrder.Items)
            {
                oi.Product = await _context.Products.Where(p => p.Id == oi.ProductId).FirstOrDefaultAsync();
            }
        }

        return _mapper.Map<OrderDto>(newOrder);
    }

    // Simple auto-increment helper that finds the current max Order.Id and returns +1.
    // This approach is acceptable for low-concurrency scenarios. For production with
    // concurrent writes, consider using a dedicated counter document or Cosmos stored
    // procedure to atomically increment.
    private async Task<int> GetNextOrderIdAsync()
    {
        // Use nullable int to handle empty collections
        var maxId = await _context.Orders
                                  .Select(o => (int?)o.Id)
                                  .MaxAsync();

        return (maxId ?? 0) + 1;
    }

}