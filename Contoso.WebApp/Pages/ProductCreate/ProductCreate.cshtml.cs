using Contoso.WebApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class ProductCreateModel : PageModel
{

        public bool IsAdmin { get; set; }

        public string ImageUrl { get; set; }

        [BindProperty]
        public ProductDto Product { get; set; }


        [BindProperty]
        public IFormFile Image { get; set; }


        public string SuccessMessage { get; set; }


        private readonly IContosoAPI _contosoApi;
        private readonly IBlobImageService _blobImageService;

        public ProductCreateModel(IContosoAPI contosoApi, IBlobImageService blobImageService)
        {
            _contosoApi = contosoApi;
            _blobImageService = blobImageService;
        }


        public void OnGet()
        {
            IsAdmin = true;
        }


        public async Task<IActionResult> OnPostCreateProductAsync()
        {
            if (Image != null && Image.Length > 0)
            {
                using (var memoryStream = new MemoryStream())
                {
                    await Image.CopyToAsync(memoryStream);
                    Product.Image = memoryStream.ToArray();
                }

                var fileName = Path.GetFileName(Image.FileName);

                // Upload directly to blob storage and set ImageUrl to blob name
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

                    // Clear large image bytes before sending product DTO to API
                    Product.Image = null;
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, "Failed to upload image: " + ex.Message);
                    return Page();
                }
            }

            var response = await _contosoApi.CreateProductAsync(Product);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Product created successfully!";
                return RedirectToPage("/ProductCreate/ProductCreate");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "An error occurred while creating the product.");
                return Page();
            }
        }
}
