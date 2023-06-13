using GrpcWizardLibrary;

[GrpcService(typeof(Product))]
public interface IProductsService
{
    Task<ProductsResponse> GetAll(GetAllProductsRequest request);
    Task<ProductResponse> GetProductById(GetProductByIdRequest request);
    Task<ProductResponse> AddProduct(ProductRequest request);
    Task<DeleteProductResponse> DeleteProduct(ProductRequest request);
}

[GrpcService(typeof(Product))]
public class ProductsService : IProductsService
{
    private List<Product> products = new List<Product>();

    public ProductsService()
    {
        products.Add(new Product
        {
            Id = 1,
            Name = "Fizzbanger",
            Description = "For those hard-to-reach-spots",
            Price = 9.99m
        });
        products.Add(new Product
        {
            Id = 2,
            Name = "Left-handed HMPTA",
            Description = "Reverse-threaded for your protection",
            Price = 19.99m
        });
        products.Add(new Product
        {
            Id = 3,
            Name = "Plastic Ficus Spray",
            Description = "Take care of your fake plants",
            Price = 4.99m
        });
    }

    public Task<ProductResponse> AddProduct(ProductRequest request)
    {
        request.Product.Id = products.Last().Id + 1;
        products.Add(request.Product);
        var reply = new ProductResponse() { Success = true, Product = request.Product };
        return Task.FromResult(reply);
    }

    public Task<DeleteProductResponse> DeleteProduct(ProductRequest request)
    {
        var id = request.Product.Id;
        var Product = (from x in products
                       where x.Id == id
                       select x).FirstOrDefault();
        if (Product == null)
        {
            return Task.FromResult(new DeleteProductResponse() { Success = false });
        }

        products.Remove(Product);

        return Task.FromResult(new DeleteProductResponse() { Success = true });
    }

    public Task<ProductsResponse> GetAll(GetAllProductsRequest request)
    {
        var reply = new ProductsResponse();
        // add the entire set to reply.Products
        reply.Products.AddRange(products);
        return Task.FromResult(reply);
    }

    public Task<ProductResponse> GetProductById(GetProductByIdRequest request)
    {
        var reply = new ProductResponse();
        // find the Product by request.Id and return
        var product = (from x in products
                      where x.Id == request.Id
                      select x).FirstOrDefault();

        if (product == null)
        {
            reply.Success = false;
        }
        else
        {
            reply.Success = true;
            reply.Product = product;
        }
        return Task.FromResult(reply);
    }
}

