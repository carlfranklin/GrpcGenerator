using GrpcWizardLibrary;

[GrpcService(typeof(Person))]
public interface IPeopleService
{
    Task<PeopleResponse> GetAll(GetAllPeopleRequest request);
    Task<PersonResponse> GetPersonById(GetPersonByIdRequest request);
    Task<PersonResponse> AddPerson(PersonRequest request);
    Task<DeletePersonResponse> DeletePerson(PersonRequest request);
}

// Grpc Services must implement an interface,
// which also must have the [GrpcService] attribute
[GrpcService(typeof(Person))]
public class PeopleService : IPeopleService
{
    private List<Person> people = new List<Person>();

    public PeopleService()
    {
        people.Add(new Person { Id = 1, FirstName = "Isadora", LastName = "Jarr" });
        people.Add(new Person { Id = 2, FirstName = "Ben", LastName = "Drinkin" });
        people.Add(new Person { Id = 3, FirstName = "Amanda", LastName = "Reckonwith" });
    }

    public Task<PersonResponse> AddPerson(PersonRequest request)
    {
        request.Person.Id = people.Last().Id + 1;
        people.Add(request.Person);
        var reply = new PersonResponse() { Success = true, Person = request.Person };
        return Task.FromResult(reply);
    }

    public Task<DeletePersonResponse> DeletePerson(PersonRequest request)
    {
        var id = request.Person.Id;
        var person = (from x in people
                      where x.Id == id
                      select x).FirstOrDefault();
        if (person == null)
        {
            return Task.FromResult(new DeletePersonResponse() { Success = false });
        }

        people.Remove(person);

        return Task.FromResult(new DeletePersonResponse() { Success = true });
    }

    public Task<PeopleResponse> GetAll(GetAllPeopleRequest request)
    {
        var reply = new PeopleResponse();
        // add the entire set to reply.People
        reply.People.AddRange(people);
        return Task.FromResult(reply);
    }

    public Task<PersonResponse> GetPersonById(GetPersonByIdRequest request)
    {
        var reply = new PersonResponse();
        // find the person by request.Id and return
        var person = (from x in people
                      where x.Id == request.Id
                      select x).FirstOrDefault();
        if (person == null)
        {
            reply.Success = false;
        }
        else
        {
            reply.Success = true;
            reply.Person = person;
        }
        return Task.FromResult(reply);
    }
}