using GrpcWizardLibrary;

[GrpcModel]
public class Person
{
    public int Id { get; set; }
    public long LongValue { get; set; }
    public UInt32 UInt32Value { get; set; }
    public bool IsValid { get; set; }
    public string[] StringArray { get; set; }
    public int[] IntArray { get; set; }
    public List<double> DoubleList { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public byte[] MyByteArray { get; set; }
}

public class GetAllPeopleRequest
{
}

public class GetPersonByIdRequest
{
    public int Id { get; set; }
}

public class PersonRequest
{
    public Person Person { get; set; }
}

public class PersonResponse
{
    public bool Success { get; set; }
    public Person Person { get; set; }
}

public class DeletePersonResponse
{
    public bool Success { get; set; }
}

public class PeopleResponse
{
    public List<Person> People { get; set; } = new List<Person>();
}

