using Contoso.Api.Data;
using Contoso.Api.Models;
using Contoso.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Azure.Storage.Blobs;

namespace Contoso.Api.Controllers;

[ApiController]
[Route("/api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductsService _productService;

    public ProductsController(IProductsService productService)
    {
        _productService = productService;
    }

    [HttpPost]
    public async Task<PagedResult<ProductDto>> GetProductsAsync(QueryParameters queryParameters)
    {
        return await _productService.GetProductsAsync(queryParameters);
    }

    [HttpGet("categories")]
    public async Task<List<string>> GetProductCategories()
    {
        return await _productService.GetProductCategories();
    }
    
    [HttpGet("{id}")]
    public async Task<ProductDto> GetProductAsync(int id)
    {
        return await _productService.GetProductAsync(id);
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<ProductDto> CreateProductAsync(ProductDto product)
    {
        return await _productService.CreateProductAsync(product);
    }


    [HttpPut]
    [Authorize]
    public async Task<IActionResult> UpdateProductAsync(ProductDto product)
    {
        var updatedProduct = await _productService.UpdateProductAsync(product);

        if (updatedProduct == null)
        {
            return BadRequest("Product not found");
        }

        return Ok(updatedProduct);
    }


    [HttpPost("upload/images")]
    [Authorize]
    public async Task<IActionResult> GetUploadBlobUrl([FromBody] List<ProductImageDto> productImage)
    {
        if (productImage == null || productImage.Count == 0) return BadRequest("No images provided.");

        // Read connection string and container name from environment (server-side).
        var conn = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        var containerName = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINERNAME") ?? Environment.GetEnvironmentVariable("AZURE_STORAGE_CONTAINER_NAME");

        // Ensure we upload to the images container by default if not configured on the server
        if (string.IsNullOrWhiteSpace(containerName))
        {
            containerName = "images";
        }

        if (string.IsNullOrWhiteSpace(conn))
        {
            return StatusCode(500, "Storage connection string not found on server.");
        }

        try
        {
            var containerClient = new Azure.Storage.Blobs.BlobContainerClient(conn, containerName);
            foreach (var img in productImage)
            {
                if (img?.Image == null || string.IsNullOrWhiteSpace(img.ImageUrl)) continue;

                var blobClient = containerClient.GetBlobClient(img.ImageUrl);
                using var ms = new System.IO.MemoryStream(img.Image);
                await blobClient.UploadAsync(ms, overwrite: true);

                if (img.Metadata != null && img.Metadata.Count > 0)
                {
                    await blobClient.SetMetadataAsync(img.Metadata);
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Failed to upload images: " + ex.Message);
        }
    }

    [HttpPost("create/bulk")]
    [Authorize]
    public async Task<IActionResult> CreateProductsAsync(List<ProductDto> products)
    {
        if (products == null || products.Count == 0)
        {
            return BadRequest("No products provided.");
        }

        try
        {
            var created = new List<ProductDto>();
            foreach (var p in products)
            {
                var createdProduct = await _productService.CreateProductAsync(p);
                created.Add(createdProduct);
            }

            return Ok(created);
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Failed to create products: " + ex.Message);
        }
    }


    [HttpDelete("{id}")]
    [Authorize]
    public async Task DeleteProductAsync(int id)
    {
        await _productService.DeleteProductAsync(id);
    }
}