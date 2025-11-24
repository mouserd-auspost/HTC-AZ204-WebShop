using Contoso.Api.Data;
using Contoso.WebApp.Data;
using Microsoft.AspNetCore.Mvc;
using Refit;
using static Contoso.WebApp.Pages.Account.LoginModel;

namespace Contoso.WebApp.Services
{
    public interface IContosoAPI
    {
        
        [Post("/api/Account/Login")]
        Task<ApiResponse<LoginDto>> LoginAsync(LoginInputModel loginInputModel);

        [Post("/api/Products")]
        Task<ApiResponse<PagedResult<ProductDto>>> GetProductsPagedAsync(QueryParameters queryParameters);

        [Get("/api/Products/{id}")]
        Task<ApiResponse<ProductDto>> GetProductAsync(int id);

        [Post("/api/Products/create")]
        Task<ApiResponse<ProductDto>> CreateProductAsync(ProductDto product);

        [Post("/api/Products/create/bulk")]
        Task<ApiResponse<IActionResult>> CreateProductsAsync(List<ProductDto> products);

        [Put("/api/Products")]
        Task<ApiResponse<IActionResult>> UpdateProductAsync(ProductDto product);

        [Post("/api/Products/upload/images")]
        Task<ApiResponse<IActionResult>> UploadImagesAsync(List<ProductImageDto> productImages);

        [Get("/api/Products/categories")]
        Task<ApiResponse<List<string>>> GetCategoriesAsync();

        

        [Post("/api/Order")]
        Task<ApiResponse<OrderDto>> SubmitOrderAsync(OrderDto orderDto);

        [Get("/api/User")]
        Task<ApiResponse<UserInfoDto>> GetUserInfoAsync();

        [Put("/api/User")]
        Task<ApiResponse<IActionResult>> UpdateUserInfoAsync(UserInfoDto userInfoDto);
    }
}