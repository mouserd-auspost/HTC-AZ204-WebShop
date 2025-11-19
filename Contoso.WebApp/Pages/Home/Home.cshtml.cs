using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Contoso.Api.Data;
using Contoso.WebApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Contoso.WebApp.Extensions;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;


public class HomeModel : PageModel
{
    public List<ProductDto> Products { get; set; }

    public List<string> Categories { get; set; }

    public int CurrentPage  { get; set; } = 1;

    public int TotalPages { get; set; }

    public int PageSize { get; set; } = 3;

    public string CategorySelected { get; set; } = "";

    public string ErrorMessage { get; set; }

    private readonly IContosoAPI _contosoAPI;
    private readonly IBlobImageService _blobImageService;
    private readonly ILogger<HomeModel> _logger;

    public HomeModel(IContosoAPI contosoAPI, IBlobImageService blobImageService, ILogger<HomeModel> logger)
    {
        _contosoAPI = contosoAPI;
        _blobImageService = blobImageService;
        _logger = logger;
    }
   
    public async Task OnGetAsync()
    {

        if (HttpContext.Session.Get("CartCount") == null) 
        {
            HttpContext.Session.Set("CartCount", 0);
        }
        
        Categories = new List<string>();
        try
        {
            var category_response = await _contosoAPI.GetCategoriesAsync();
            Categories = category_response.Content ?? new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve categories.");
        }
    
        bool isCategorySelected = HttpContext.Session.Get<string>("CategorySelected") != null;
        bool isPageSelected = HttpContext.Session.Get<int>("CurrentPage") > 0;

        if (isCategorySelected)
        {
            CategorySelected = HttpContext.Session.Get<string>("CategorySelected");
        }

        if (isPageSelected)
        {
            CurrentPage = HttpContext.Session.Get<int>("CurrentPage");
        }


        try
        {
            var pagedProducts = await GetPagedFilteredProduct(CurrentPage, CategorySelected ?? string.Empty);
            if (pagedProducts != null)
            {
                Products = pagedProducts.Items ?? new List<ProductDto>();
                TotalPages = pagedProducts.PageSize > 0 ? (int)Math.Ceiling((double)pagedProducts.TotalCount / pagedProducts.PageSize) : 0;
            }
            else
            {
                Products = new List<ProductDto>();
                TotalPages = 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve products page {Page}.", CurrentPage);
            Products = new List<ProductDto>();
            TotalPages = 0;
            ErrorMessage = "Failed to load products.";
        }

        // Resolve blob-based display image URLs with ReleaseDate logic.
        if (Products != null)
        {
            foreach (var p in Products)
            {
                try
                {
                    p.DisplayImageUrl = await _blobImageService.GetDisplayImageUrlAsync(p.ImageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed generating image URL for product {ProductId}.", p.Id);
                }
            }
        }

    }

    public IActionResult OnPostImageClick(int productId)
    {
        return RedirectToPage("/Product/Product", new { id = productId });
    }

    public IActionResult OnGetPage(int pageNumber)
    {
        HttpContext.Session.Set("CurrentPage", pageNumber);

        return RedirectToPage();
    }

    public IActionResult OnGetFilterByCategory(string category)
    {

        HttpContext.Session.Set("CategorySelected", category);
        HttpContext.Session.Set("CurrentPage", 1);

        Console.WriteLine("CategorySelected: " + category);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetUploadImagesAsync()
    {
         var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");
        var imageFiles = Directory.GetFiles(imagesPath);
        var productImages = new List<ProductImageDto>();

        foreach (var imageFile in imageFiles)
        {
            var imageName = Path.GetFileName(imageFile);
            var imageData = await System.IO.File.ReadAllBytesAsync(imageFile);

            var productImage = new ProductImageDto
            {
                ImageUrl = imageName,
                Image = imageData
            };

            productImages.Add(productImage);
        }

        var response = await _contosoAPI.UploadImagesAsync(productImages);

        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "Images uploaded successfully";
        }
        else
        {
            TempData["ErrorMessage"] = "Error uploading images";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetBulkUploadProducts()
    {
        var response = await _contosoAPI.CreateProductsAsync();

        if (response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = "Product Bulk uploaded was successfull";
        }
        else
        {
            TempData["ErrorMessage"] = "Error bulk uploading products";
        }
        
        return RedirectToPage();
    }

    private async Task<PagedResult<ProductDto>> GetPagedFilteredProduct(int pageNumber, string category)
    {
        var productResponse = await _contosoAPI.GetProductsPagedAsync(new QueryParameters
        {
            filterText = category,
            PageNumber = pageNumber,
            PageSize = PageSize,
            StartIndex = (pageNumber - 1) * PageSize
        });

        return productResponse.Content;
    }

}