using GrpcWizardLibrary;
using System.Reflection;

// Test GetAll():
Console.WriteLine("Testing GetAll():");
var service = new PeopleService();
var response = await service.GetAll(new GetAllPeopleRequest());
foreach (var person in response.People)
{
    Console.WriteLine($"    ({person.Id}) {person.FirstName} " +
        $"{person.LastName}");
}

// Test AddPerson():
Console.WriteLine("Testing AddPerson:");
var addPersonRequest = new PersonRequest();
addPersonRequest.Person = new Person()
{
    FirstName = "Hugh",
    LastName = "Jass"
};
var addResponse = await service.AddPerson(addPersonRequest);
if (!addResponse.Success)
{
    Console.WriteLine("Person could not be added");
}
else
{
    var person = addResponse.Person;
    Console.WriteLine($"    ({person.Id}) {person.FirstName} " +
        $"{person.LastName}");
}

// Test GetPersonById(4):
Console.WriteLine("Testing GetPersonById(4):");
var request = new GetPersonByIdRequest() { Id = 4 };
var getPersonResponse = await service.GetPersonById(request);
if (!getPersonResponse.Success)
{
    Console.WriteLine("Person could not be retreieved");
}
else
{
    var person = getPersonResponse.Person;
    Console.WriteLine($"    ({person.Id}) {person.FirstName} " +
        $"{person.LastName}");
}

// Test DeletePerson(4):
Console.WriteLine("Testing DeletePerson(4):");
var deletePersonRequest = new PersonRequest();
deletePersonRequest.Person = addResponse.Person;
var deletePersonResponse = await service.DeletePerson
    (deletePersonRequest);
if (deletePersonResponse.Success)
{
    response = await service.GetAll(new GetAllPeopleRequest());
    foreach (var person in response.People)
    {
        Console.WriteLine($"    ({person.Id}) {person.FirstName} " +
            $"{person.LastName}");
    }
}
else
{
    var person = addResponse.Person;
    Console.WriteLine($"{person.FirstName} {person.LastName} " +
        $"could not be deleted.");
}

// END TEST CODE

// Collect the NameSpace for the generated code
Console.Write("Enter the project namespace or ENTER for 'BlazorGrpcGenerated': ");
var nameSpace = Console.ReadLine();
if (nameSpace == "") nameSpace = "BlazorGrpcGenerated";

// Specify the folder where generated files will be written
string outputFolder = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\Output\\";
//string outputFolder = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\Output\\BlazorGrpcGenerated\\BlazorGrpcGenerated\\";

Console.Write("Generating...");

// This returns a string with all the output data
string result = await GrpcWizard.GenerateGrpcInfrastructure
    (Assembly.GetExecutingAssembly(), 
        nameSpace, outputFolder);

Console.WriteLine();
Console.WriteLine(result);

