using System.Threading.Tasks;
using Contoso.WebApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Contoso.WebApp.Extensions;
public class ProductModel : PageModel
{
    private readonly IContosoAPI _contosoAPI;
    private readonly IBlobImageService _blobImageService;

    public ProductDto Product { get; set; }

    public string ErrorMessage { get; set; }

    public string SuccessMessage { get; set; }

    public bool isAdmin { get; set; }

    public string? DisplayImageUrl { get; set; }

    public ProductModel(IContosoAPI contosoAPI, IBlobImageService blobImageService)
    {
        _contosoAPI = contosoAPI;
        _blobImageService = blobImageService;
    }
   
    public async Task OnGetAsync(int id)
    {
        Console.WriteLine("ProductModel.OnGetAsync");
        
        var response = await _contosoAPI.GetProductAsync(id);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Failed to retrieve product";
        }

        if (HttpContext.Session.Get<string>("SuccessMessage") != null)
        {
            SuccessMessage = HttpContext.Session.Get<string>("SuccessMessage") ?? string.Empty;
            HttpContext.Session.Remove("SuccessMessage");
        }

        Product = response.Content;
        if (Product != null)
        {
            DisplayImageUrl = await _blobImageService.GetDisplayImageUrlAsync(Product.ImageUrl);
        }

        isAdmin = true;
    }

    public async Task<IActionResult> OnPostAddToCart(int id)
    {

        var response = await _contosoAPI.GetProductAsync(id);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = "Failed to retrieve product";
            return Page();
        }

        Product = response.Content;
        if (Product != null)
        {
            DisplayImageUrl = await _blobImageService.GetDisplayImageUrlAsync(Product.ImageUrl);
        }

        List<OrderItemDto> orderItems = HttpContext.Session.Get<List<OrderItemDto>>("OrderItems") ?? new List<OrderItemDto>();

        var existingOrderItem = orderItems.FirstOrDefault(oi => oi.ProductId == id);

        if (existingOrderItem != null)
        {
            existingOrderItem.Quantity++;
        }
        else
        {
            orderItems.Add(new OrderItemDto
            {
                ProductId = id,
                Quantity = 1,
                Price = Product.Price,
                Product = Product
            });
        }

        int cartCount = HttpContext.Session.Get<int>("CartCount");
        
        HttpContext.Session.Set("OrderItems", orderItems);

        HttpContext.Session.Set("SuccessMessage", "Product added to cart");

        HttpContext.Session.Set("CartCount", cartCount + 1);

        return RedirectToPage(new { id });
    }
}