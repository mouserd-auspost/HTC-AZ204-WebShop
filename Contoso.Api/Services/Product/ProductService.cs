using System.Runtime.CompilerServices;
using AutoMapper;
using Contoso.Api.Data;
using Contoso.Api.Models;
using Microsoft.EntityFrameworkCore;
using Azure.Messaging.EventGrid;
using Azure;
using Microsoft.Extensions.Configuration;

namespace Contoso.Api.Services;

public class ProductsService : IProductsService
{
    private readonly ContosoDbContext _context;
    private readonly IMapper _mapper;
    private readonly EventGridPublisherClient? _eventGridClient;
    private readonly IConfiguration _configuration;

    public ProductsService(ContosoDbContext context, IMapper mapper, IConfiguration configuration)
    {
        _context = context;
        _mapper = mapper;
        _configuration = configuration;

        // Configure Event Grid publisher if configured
        var topicEndpoint = _configuration["EventGrid:ProductUpdatedTopicEndpoint"];
        var topicKey = _configuration["EventGrid:ProductUpdatedTopicKey"];

        if (!string.IsNullOrEmpty(topicEndpoint) && !string.IsNullOrEmpty(topicKey))
        {
            try
            {
                _eventGridClient = new EventGridPublisherClient(new Uri(topicEndpoint), new AzureKeyCredential(topicKey));
            }
            catch
            {
                // If client can't be created, leave null so publishing is optional and doesn't break the update flow
                _eventGridClient = null;
            }
        }
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(QueryParameters queryParameters)
    {
        var products = await _context.Products
                            .Where(p =>  p.Category == queryParameters.filterText || string.IsNullOrEmpty(queryParameters.filterText))
                            .Skip(queryParameters.StartIndex) 
                            .Take(queryParameters.PageSize)
                            .ToListAsync();

        var totalCount = await _context.Products
                                        .Where(p =>  p.Category == queryParameters.filterText || string.IsNullOrEmpty(queryParameters.filterText))
                                        .CountAsync();

        var pagedProducts = new PagedResult<ProductDto>
        {
            Items = _mapper.Map<List<ProductDto>>(products),
            TotalCount = totalCount,
            PageSize = queryParameters.PageSize,
            PageNumber = queryParameters.PageNumber
        };


        return pagedProducts;
    }

    public async Task<ProductDto> GetProductAsync(int id)
    {
        var product = await _context.Products.FindAsync(id);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> CreateProductAsync(ProductDto product)
    {
        var productModel = _mapper.Map<Product>(product);

        _context.Products.Add(productModel);

        await _context.SaveChangesAsync();

        return _mapper.Map<ProductDto>(productModel);
    }

    public async Task<ProductDto> UpdateProductAsync(ProductDto product)
    {
        var existingProduct = await _context.Products.AsNoTracking().FirstAsync(x => x.Id == product.Id);

        if  (existingProduct == null)
        {
            return null;
        }

        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.Price = product.Price;
        
        if (existingProduct.ImageUrl != product.ImageUrl)
        {
            existingProduct.ImageUrl = product.ImageUrl;
        }


        _context.Entry(existingProduct).State = EntityState.Modified;

        await _context.SaveChangesAsync();

        // Publish ProductUpdated event to Event Grid (optional)
        if (_eventGridClient != null)
        {
            try
            {
                var eventData = new
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Category = product.Category,
                    ImageUrl = product.ImageUrl
                };

                var evt = new EventGridEvent(
                    subject: $"Products/{product.Id}",
                    eventType: "Contoso.Product.Updated",
                    dataVersion: "1.0",
                    data: eventData
                );

                await _eventGridClient.SendEventsAsync(new[] { evt });
            }
            catch
            {
                // Swallow exceptions to avoid breaking the product update flow if Event Grid publishing fails
            }
        }

        return _mapper.Map<ProductDto>(existingProduct);
    }

    public async Task DeleteProductAsync(int id)
    {
        var product = await _context.Products.AsNoTracking().FirstAsync(x => x.Id == id);

        _context.Products.Remove(product);

        await _context.SaveChangesAsync();
    }

    public async Task<List<string>> GetProductCategories()
    {
        return await _context.Products.Select(x => x.Category).Distinct().ToListAsync();
    }
}