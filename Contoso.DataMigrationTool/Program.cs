
using System.Threading.Tasks;
using Contoso.Api.Models;
using Microsoft.Azure.Cosmos;
using Azure.Core;
using Azure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {

        string currentDirectory = System.IO.Directory.GetCurrentDirectory();

        var config = new ConfigurationBuilder()
            .SetBasePath(currentDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        await TransferDataToCosmos(config);
    }

    static async Task TransferDataToCosmos(IConfigurationRoot config)
    {
            var optionsBuilder = new DbContextOptionsBuilder<ContosoDbContext>();
            optionsBuilder.UseSqlServer(config["ConnectionStrings:ContosoDBConnection"]);

            using var sqlContext = new ContosoDbContext(optionsBuilder.Options);

            var accountEndpoint = config["Azure:CosmosDB:AccountEndpoint"];
            var databaseName = config["Azure:CosmosDB:DatabaseName"];
            CosmosClient cosmosClient = new CosmosClient(accountEndpoint, new DefaultAzureCredential());
            var database = cosmosClient.GetDatabase(databaseName);

            var container = database.GetContainer("Users");

            // Handle users

            var users = await sqlContext.Users.ToListAsync();

            foreach (var user in users)
            {
                var cosmosUser = new 
                {
                    id = user.Id.ToString(),
                    user.Id,
                    user.Name,
                    user.Email,
                    user.Address,
                    user.PasswordHash,
                    user.CreatedAt
                };

                await container.CreateItemAsync(cosmosUser, new PartitionKey(user.Email));
            }

            // Handle products

            var products = await sqlContext.Products.ToListAsync();

            container = database.GetContainer("Products");

            foreach (var product in products)
            {
                var cosmosProduct = new 
                {
                    id = product.Id.ToString(),
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Price,
                    product.ImageUrl,
                    product.Category,
                    product.CreatedAt
                };

                await container.CreateItemAsync(cosmosProduct, new PartitionKey(product.Category));
            }

            // Handle orders

            var orders = await sqlContext.Orders
                                        .Include(o => o.Items)
                                        .ToListAsync();

            container = database.GetContainer("Orders");

            foreach (var order in orders)
            {
                var cosmosOrder = new 
                {
                    id = order.Id.ToString(),
                    order.Id,
                    order.UserId,
                    order.Status,
                    order.Total,
                    order.CreatedAt,
                    Items = order.Items.Select(i => new 
                    {
                        id = i.Id.ToString(),
                        i.Id,
                        i.OrderId,
                        i.ProductId,
                        i.UnitPrice,
                        i.Quantity,
                    })
                };

                await container.CreateItemAsync(cosmosOrder, new PartitionKey(order.UserId));
            }
    }
}
