using Contoso.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

public class ProductCreateMultipleModel : PageModel
{
    public bool IsAdmin { get; set; }

    [BindProperty]
    public List<ProductDto> Products { get; set; } = new List<ProductDto>();

    [BindProperty]
    public List<IFormFile> Images { get; set; } = new List<IFormFile>();

    private readonly IContosoAPI _contosoApi;
    private readonly IBlobImageService _blobImageService;
    private readonly ILogger<ProductCreateMultipleModel> _logger;

    public ProductCreateMultipleModel(IContosoAPI contosoApi, IBlobImageService blobImageService, ILogger<ProductCreateMultipleModel> logger)
    {
        _contosoApi = contosoApi;
        _blobImageService = blobImageService;
        _logger = logger;
    }

    public void OnGet()
    {
        IsAdmin = true;
    }

    public async Task<IActionResult> OnPostCreateProductsAsync()
    {
        _logger.LogInformation("OnPostCreateProductsAsync started. Product count: {Count}", Products?.Count ?? 0);

        if (Products == null || Products.Count == 0)
        {
            _logger.LogWarning("No products submitted in OnPostCreateProductsAsync");
            ModelState.AddModelError(string.Empty, "No products to create.");
            return Page();
        }

        var errors = new List<string>();

        // Upload images (if provided) and set ImageUrl for each product in the corresponding index
        for (int i = 0; i < Products.Count; i++)
        {
            var product = Products[i];
            product.Id = i;
            _logger.LogInformation("Processing product {Index}: Name={Name}", i + 1, product?.Name ?? "(null)");

            if (Images != null && i < Images.Count && Images[i] != null && Images[i].Length > 0)
            {
                try
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await Images[i].CopyToAsync(memoryStream);
                        product.Image = memoryStream.ToArray();
                    }

                    var fileName = Path.GetFileName(Images[i].FileName);
                    IDictionary<string, string>? metadata = null;
                    if (product.ReleaseDate.HasValue)
                    {
                        metadata = new Dictionary<string, string>
                        {
                            { "ReleaseDate", product.ReleaseDate.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") }
                        };
                    }

                    await _blobImageService.UploadImageAsync(fileName, product.Image, metadata);
                    product.ImageUrl = fileName;
                    product.Image = null;
                    _logger.LogInformation("Uploaded image for product {Index}: {FileName}", i + 1, fileName);
                }
                catch (Exception ex)
                {
                    errors.Add($"Product {i + 1}: failed to upload image: {ex.Message}");
                    _logger.LogError(ex, "Product {Index}: failed to upload image", i + 1);
                }
            }
            else
            {
                _logger.LogInformation("No image provided for product {Index}", i + 1);
            }
        }

        // If any image upload errors occurred, return the page with errors
        if (errors.Count > 0)
        {
            ModelState.AddModelError(string.Empty, string.Join("; ", errors));
            return Page();
        }

        // Call bulk API once with the prepared product list
        _logger.LogInformation("Calling bulk create API for {Count} products", Products.Count);
        var apiResponse = await _contosoApi.CreateProductsAsync(Products);

        if (apiResponse.IsSuccessStatusCode)
        {
            _logger.LogInformation("Bulk create succeeded with status {Status}", apiResponse.StatusCode);
            TempData["SuccessMessage"] = "Products created successfully!";
            return RedirectToPage("/ProductCreate/ProductCreateMultiple");
        }
        else
        {
            _logger.LogError("Bulk create failed with status {Status}", apiResponse.StatusCode);
            ModelState.AddModelError(string.Empty, "An error occurred while creating the products.");
            return Page();
        }
    }
}
