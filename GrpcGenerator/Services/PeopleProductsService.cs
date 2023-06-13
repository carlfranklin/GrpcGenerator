using GrpcWizardLibrary;

[GrpcService(typeof(Person), typeof(Product))]
public interface IPeopleProductsService
{
    Task<PeopleResponse> GetAllPeople(GetAllPeopleRequest request);
    Task<ProductsResponse> GetAllProducts(GetAllProductsRequest request);
}

[GrpcService(typeof(Person), typeof(Product))]
public class PeopleProductsService : IPeopleProductsService
{
    private List<Person> people = new List<Person>();
    private List<Product> products = new List<Product>();

    public PeopleProductsService()
    {
        people.Add(new Person { Id = 1, FirstName = "Isadora", LastName = "Jarr" });
        people.Add(new Person { Id = 2, FirstName = "Ben", LastName = "Drinkin" });
        people.Add(new Person { Id = 3, FirstName = "Amanda", LastName = "Reckonwith" });

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

    public Task<PeopleResponse> GetAllPeople(GetAllPeopleRequest request)
    {
        var response = new PeopleResponse();
        // add the entire set to reply.People
        response.People.AddRange(people);
        return Task.FromResult(response);
    }

    public Task<ProductsResponse> GetAllProducts(GetAllProductsRequest request)
    {
        var response = new ProductsResponse();
        // add the entire set to reply.People
        response.Products.AddRange(products);
        return Task.FromResult(response);
    }
}
