using GrpcWizardLibrary;

[GrpcModel]
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Price { get; set; }
}

public class GetAllProductsRequest
{
}

public class GetProductByIdRequest
{
    public int Id { get; set; }
}

public class ProductRequest
{
    public Product Product { get; set; }
}

public class ProductResponse
{
    public bool Success { get; set; }
    public Product Product { get; set; }
}

public class DeleteProductResponse
{
    public bool Success { get; set; }
}

public class ProductsResponse
{
    public List<Product> Products { get; set; } = new List<Product>();
}

