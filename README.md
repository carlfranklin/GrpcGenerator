# GrpcGenerator

GrpcGenerator is a .NET 6 console app that demonstrates the **GrpcWizard** library, which generates a complete gRPC infrastructure for a hosted Blazor WebAssembly application from a simple service and an interface. Each method in the service must define an input type and return a Task of an output type. 

You will end up with a client-side service that uses .NET types.

Conversion to and from gRPC message types is done automatically.

The wizard will generate:

- a proto file
- converter class files to convert gRPC message types to .NET types and vice-versa
- a gRPC server-side service that calls into your existing service 
- a client-side service that calls the gRPC service, converting types automatically
- a README.txt file that has snippets to add to existing files in all three projects

## Example:

Let's start with a simple model.

```c#
public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Bio { get; set; } = "";
    public string PhotoUrl { get; set; } = "";
}
```

Before you can generate the gRPC code, you need an interface with at least one method that accepts a custom type and returns a custom type. You can not use primitive types like string or Int32. If you need to pass those, create a class around them.

Let's create some simple classes we can use to send and return data. We're going to add these to a Models folder in the Shared project:

```c#
public class GetAllPeopleRequest
{
    // If you want to pass nothing, you still have to wrap a class around it.
}
```

```c#
public class PeopleReply
{
    public List<Person> People { get; set; } = new List<Person>();
}
```

```c#
public class GetPersonByIdRequest
{
    public int Id { get; set; }
}
```

Now we can create a service interface in the Server project:

```c#
[GrpcService]
public interface IPeopleService
{
    Task<PeopleReply> GetAll(GetAllPeopleRequest request);
    Task<Person> GetPersonById(GetPersonByIdRequest request);
}
```

Notice that the interface is decorated with the `[GrpcService]` attribute. That's so the **GrpcWizard** can tell which members of the service are relevant.

Next, we create an implementation of `IPeopleService`. Note that the service must ALSO be decorated with the `[GrpcService]` attribute.

```c#
[GrpcService]
public class PeopleService : IPeopleService
{
    private List<Person> people = new List<Person>();

    public PeopleService()
    {
        people.Add(new Person { Id = 1, FirstName = "Isadora", 
                               LastName = "Jarr" });
        people.Add(new Person { Id = 2, FirstName = "Ben", 
                               LastName = "Drinkin" });
        people.Add(new Person { Id = 3, FirstName = "Amanda", 
                               LastName = "Reckonwith" });
    }
    
    public Task<PeopleReply> GetAll(GetAllPeopleRequest request)
    {
        var reply = new PeopleReply();
        // add the entire set to reply.People
        reply.People.AddRange(people);
        return Task.FromResult(reply);
    }

    public Task<Person> GetPersonById(GetPersonByIdRequest request)
    {
        // find the person by request.Id and return
        var result = (from x in people
              where x.Id == request.Id
              select x).FirstOrDefault();

        return Task.FromResult(result);
    }
}
```

Now you are ready to generate! Here's what the GrpcGenerator console app does:

```c#
// This service and its interface must be decorated with 
// the [GrpcService] attribute
var ServiceType = typeof(PeopleService);

// the namespace where the protobuf objects will reside
var ModelsFolder = "BlazorGrpcGenerated.Shared.Models";

// the name prefix of the service
var ServiceName = "People";

// the name of the proto file to generate
var ProtoFileName = "people.proto";

// where generated files will be written
string OutputFolder = @"c:\users\carlf\desktop\Output\";

Console.Write("Generating...");

// This returns a string with all the output data
string result = await GrpcWizard.GenerateGrpcInfrastructure(
	ServiceType,
	ModelsFolder,
	ServiceName,
	ProtoFileName,
	OutputFolder);

Console.WriteLine();
Console.WriteLine(result);
```

In the Output folder you will see a README.txt file with snippets to put into your existing files: .csproj files, Startup.cs, Program.cs, etc.

You'll also see a *Client* subfolder, a *Server* subfolder, and a *Shared* subfolder containing generated files that you must copy into those projects.

## Demo Output:

### README.txt

```
Instructions for modifying your Blazor WebAssembly app to support gRPC

Shared Project:
===============
1) Add the following to the Shared project .csproj file:

    <ItemGroup>
        <PackageReference Include="Google.Protobuf" Version="3.15.8" />
        <PackageReference Include="Grpc.Net.Client" Version="2.36.0" />
        <PackageReference Include="Grpc.Tools" Version="2.37.0" >
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
    </ItemGroup>
    <ItemGroup>
        <SupportedPlatform Include="browser" />
        <Protobuf Include="people.proto" />
    </ItemGroup>

2) Add the people.proto file to the Shared project.

3) Add the following converter files to the Shared project:

   GetAllPeopleRequestConverter.cs
   PeopleReplyConverter.cs
   GetPersonByIdRequestConverter.cs
   PersonConverter.cs


Server Project:
===============
1) Add the following to the Server project .csproj file:

    <ItemGroup>
        <PackageReference Include="Grpc.AspNetCore" Version="2.36.0" />
        <PackageReference Include="Grpc.AspNetCore.Web" Version="2.36.0" />
    </ItemGroup>

2) Add the Grpc_PeopleService.cs file to the Server project.

3) Add the following to the Server project Startup.cs file:

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddGrpc();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseGrpcWeb(); // goes after app.UseRouting()
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGrpcService<Grpc_PeopleService>().EnableGrpcWeb();
        });
    }


Client Project:
===============
1) Add the following to the Client project .csproj file:

    <ItemGroup>
        <PackageReference Include="Grpc.Net.Client.Web" Version="2.36.0" />
    </ItemGroup>

2) Add the GrpcPeopleClient.cs file to the Client project.

3) Add the following to the Client project Program.cs file:

    using BlazorGrpcGenerated.Shared.Models;
    using Grpc.Net.Client;
    using Grpc.Net.Client.Web;

    public static async Task Main(string[] args)
    {
        builder.Services.AddSingleton(services =>
        {
            var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));
            var baseUri = builder.HostEnvironment.BaseAddress;
            var channel = GrpcChannel.ForAddress(baseUri, new GrpcChannelOptions { HttpClient = httpClient });
            return new Grpc_People.Grpc_PeopleClient(channel);
        });
        builder.Services.AddScoped<GrpcPeopleClient>();
    }

4) Add the following @using statement to the Client project _Imports.razor file:
     @using BlazorGrpcGenerated.Shared.Models

5) Add the following to the top of any .razor file to access data:
    @inject GrpcPeopleClient PeopleClient
```

### Shared\people.proto

This is the proto file that gRPC uses to define the interface to the gRPC service.

```c#
syntax = "proto3";
option csharp_namespace = "BlazorGrpcGenerated.Shared.Models";

service Grpc_People {
    rpc GetAll (Grpc_GetAllPeopleRequest) returns (Grpc_PeopleReply);
    rpc GetPersonById (Grpc_GetPersonByIdRequest) returns (Grpc_Person);
}

message Grpc_GetAllPeopleRequest {
}

message Grpc_PeopleReply {
    repeated Grpc_Person people = 1;
}

message Grpc_GetPersonByIdRequest {
    int32 id = 1;
}

message Grpc_Person {
    int32 id = 1;
    string firstName = 2;
    string lastName = 3;
    string bio = 4;
    string photoUrl = 5;
}
```

### Server\Grpc_PeopleService.cs

This is an implementation of the gRPC service in the Server project. Note that it calls into your existing PeopleService to access the data.

```c#
using Grpc.Core;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrpcGenerator;

namespace BlazorGrpcGenerated.Shared.Models
{
    public class Grpc_PeopleService : Grpc_People.Grpc_PeopleBase
    {
        PeopleService peopleService;

        public Grpc_PeopleService(PeopleService _peopleService)
        {
            peopleService = _peopleService;
        }

        public override async Task<Grpc_PeopleReply> GetAll(Grpc_GetAllPeopleRequest request, ServerCallContext context)
        {
            var baseRequest = GetAllPeopleRequestConverter.FromGrpc_GetAllPeopleRequest(request);
            var baseResponse = await peopleService.GetAll(baseRequest);
            var response = PeopleReplyConverter.FromPeopleReply(baseResponse);
            return response;
        }

        public override async Task<Grpc_Person> GetPersonById(Grpc_GetPersonByIdRequest request, ServerCallContext context)
        {
            var baseRequest = GetPersonByIdRequestConverter.FromGrpc_GetPersonByIdRequest(request);
            var baseResponse = await peopleService.GetPersonById(baseRequest);
            var response = PersonConverter.FromPerson(baseResponse);
            return response;
        }

    }
}
```

### Client\GrpcPeopleClient.cs

This file is used in the Client application to access the gRPC service. Note that it accepts and returns your .NET object types. It converts those .NET types to gRPC types before calling the service, and also converts the return values back to .NET.

```c#
using BlazorGrpcGenerated.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class GrpcPeopleClient
{
    Grpc_People.Grpc_PeopleClient grpc_PeopleClient;
    public GrpcPeopleClient(Grpc_People.Grpc_PeopleClient _grpc_PeopleClient)
    {
        grpc_PeopleClient = _grpc_PeopleClient;
    }

    public async Task<PeopleReply> GetAllAsync(GetAllPeopleRequest request)
    {
        var getAllPeopleRequest = GetAllPeopleRequestConverter.FromGetAllPeopleRequest(request);
        var peopleReply = await grpc_PeopleClient.GetAllAsync(getAllPeopleRequest);
        return PeopleReplyConverter.FromGrpc_PeopleReply(peopleReply);
    }

    public async Task<Person> GetPersonByIdAsync(GetPersonByIdRequest request)
    {
        var getPersonByIdRequest = GetPersonByIdRequestConverter.FromGetPersonByIdRequest(request);
        var person = await grpc_PeopleClient.GetPersonByIdAsync(getPersonByIdRequest);
        return PersonConverter.FromGrpc_Person(person);
    }

}
```

## Converters

These converters are generated as well. They convert between .NET types and gRPC message types. You can use them yourself if you like. The Client service uses them so that your code can work with .NET types. My tests have shown little to no overhead added by the conversion process.

### Shared\GetAllPeopleRequestConverter.cs

```c#
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlazorGrpcGenerated.Shared.Models;

namespace BlazorGrpcGenerated.Shared.Models
{
    public static class GetAllPeopleRequestConverter
    {
        public static List<Grpc_GetAllPeopleRequest> FromGetAllPeopleRequestList(List<GetAllPeopleRequest> list)
        {
            var result = new List<Grpc_GetAllPeopleRequest>();
            foreach (var item in list)
            {
                result.Add(FromGetAllPeopleRequest(item));
            }
            return result;
        }

        public static List<GetAllPeopleRequest> FromGrpc_GetAllPeopleRequestList(List<Grpc_GetAllPeopleRequest> list)
        {
            var result = new List<GetAllPeopleRequest>();
            foreach (var item in list)
            {
                result.Add(FromGrpc_GetAllPeopleRequest(item));
            }
            return result;
        }

        public static Grpc_GetAllPeopleRequest FromGetAllPeopleRequest(GetAllPeopleRequest item)
        {
            var result = new Grpc_GetAllPeopleRequest();
            return result;
        }


        public static GetAllPeopleRequest FromGrpc_GetAllPeopleRequest(Grpc_GetAllPeopleRequest item)
        {
            var result = new GetAllPeopleRequest();
            return result;
        }

    }
}
```

### Shared\GetPersonByIdRequestConverter.cs

```c#
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlazorGrpcGenerated.Shared.Models;

namespace BlazorGrpcGenerated.Shared.Models
{
    public static class GetPersonByIdRequestConverter
    {
        public static List<Grpc_GetPersonByIdRequest> FromGetPersonByIdRequestList(List<GetPersonByIdRequest> list)
        {
            var result = new List<Grpc_GetPersonByIdRequest>();
            foreach (var item in list)
            {
                result.Add(FromGetPersonByIdRequest(item));
            }
            return result;
        }

        public static List<GetPersonByIdRequest> FromGrpc_GetPersonByIdRequestList(List<Grpc_GetPersonByIdRequest> list)
        {
            var result = new List<GetPersonByIdRequest>();
            foreach (var item in list)
            {
                result.Add(FromGrpc_GetPersonByIdRequest(item));
            }
            return result;
        }

        public static Grpc_GetPersonByIdRequest FromGetPersonByIdRequest(GetPersonByIdRequest item)
        {
            var result = new Grpc_GetPersonByIdRequest();
            result.Id = item.Id;
            return result;
        }


        public static GetPersonByIdRequest FromGrpc_GetPersonByIdRequest(Grpc_GetPersonByIdRequest item)
        {
            var result = new GetPersonByIdRequest();
            result.Id = item.Id;
            return result;
        }

    }
}
```

### Shared\PeopleReplyConverter.cs

```c#
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlazorGrpcGenerated.Shared.Models;

namespace BlazorGrpcGenerated.Shared.Models
{
    public static class PeopleReplyConverter
    {
        public static List<Grpc_PeopleReply> FromPeopleReplyList(List<PeopleReply> list)
        {
            var result = new List<Grpc_PeopleReply>();
            foreach (var item in list)
            {
                result.Add(FromPeopleReply(item));
            }
            return result;
        }

        public static List<PeopleReply> FromGrpc_PeopleReplyList(List<Grpc_PeopleReply> list)
        {
            var result = new List<PeopleReply>();
            foreach (var item in list)
            {
                result.Add(FromGrpc_PeopleReply(item));
            }
            return result;
        }

        public static Grpc_PeopleReply FromPeopleReply(PeopleReply item)
        {
            var result = new Grpc_PeopleReply();
            var people = PersonConverter.FromPersonList(item.People.ToList());
            result.People.AddRange(people);
            return result;
        }


        public static PeopleReply FromGrpc_PeopleReply(Grpc_PeopleReply item)
        {
            var result = new PeopleReply();
            var people = PersonConverter.FromGrpc_PersonList(item.People.ToList());
            result.People.AddRange(people);
            return result;
        }

    }
}
```

### Shared\PersonConverter.cs

```c#
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlazorGrpcGenerated.Shared.Models;

namespace BlazorGrpcGenerated.Shared.Models
{
    public static class PersonConverter
    {
        public static List<Grpc_Person> FromPersonList(List<Person> list)
        {
            var result = new List<Grpc_Person>();
            foreach (var item in list)
            {
                result.Add(FromPerson(item));
            }
            return result;
        }

        public static List<Person> FromGrpc_PersonList(List<Grpc_Person> list)
        {
            var result = new List<Person>();
            foreach (var item in list)
            {
                result.Add(FromGrpc_Person(item));
            }
            return result;
        }

        public static Grpc_Person FromPerson(Person item)
        {
            var result = new Grpc_Person();
            result.Id = item.Id;
            result.FirstName = item.FirstName;
            result.LastName = item.LastName;
            result.Bio = item.Bio;
            result.PhotoUrl = item.PhotoUrl;
            return result;
        }


        public static Person FromGrpc_Person(Grpc_Person item)
        {
            var result = new Person();
            result.Id = item.Id;
            result.FirstName = item.FirstName;
            result.LastName = item.LastName;
            result.Bio = item.Bio;
            result.PhotoUrl = item.PhotoUrl;
            return result;
        }

    }
}
```

