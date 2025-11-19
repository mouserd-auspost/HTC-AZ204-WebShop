using Contoso.WebApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class ProductEditModel : PageModel
{
        private readonly IContosoAPI _contosoAPI;
        private readonly IBlobImageService _blobImageService;
        
        public bool isAdmin { get; set; } = true;

        [BindProperty]
        public ProductDto Product { get; set; }

        [BindProperty]
        public IFormFile Image { get; set; } 

        public string ErrorMessage { get; set; }

        public ProductEditModel(IContosoAPI contosoAPI, IBlobImageService blobImageService)
        {
            _contosoAPI = contosoAPI;
            _blobImageService = blobImageService;
        }

        public async Task OnGetAsync(int id)
        {

            var productResponse = await _contosoAPI.GetProductAsync(id);

            if (!productResponse.IsSuccessStatusCode)
            {
                ErrorMessage = "Failed to retrieve product";
                return;
            }

            Product = productResponse.Content;
        }


        // Implementiraj logiku da se provjerava da li je korisnik promijenio sliku
        public async Task<IActionResult>  OnPostEditProductAsync(int productId,string initialProductUrl)
        {
            // Set URL to initial URL
            Product.ImageUrl = initialProductUrl;

            if (Image != null && Image.Length > 0)
            {
                string fileName = Path.GetFileName(Image.FileName);

                using (var memoryStream = new MemoryStream())
                {
                    await Image.CopyToAsync(memoryStream);
                    Product.Image = memoryStream.ToArray();
                }

                try
                {
                    IDictionary<string, string>? metadata = null;
                    if (Product.ReleaseDate.HasValue)
                    {
                        metadata = new Dictionary<string, string>
                        {
                            { "ReleaseDate", Product.ReleaseDate.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") }
                        };
                    }

                    await _blobImageService.UploadImageAsync(fileName, Product.Image, metadata);
                    Product.ImageUrl = fileName;
                    Product.Image = null;
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Failed to upload image: " + ex.Message;
                    return Page();
                }
            }

            Product.Id = productId;
            Product.Name = Product.Name;
            Product.Price = Product.Price;

            var response = await _contosoAPI.UpdateProductAsync(Product);

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "Failed to update product";
                return Page();
            }

            TempData["SuccessMessage"] = "Product updated successfully";

            return RedirectToPage("/ProductEdit/ProductEdit", new { id = productId });
        }

}

