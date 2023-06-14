using System.Reflection;
using System.Text;
using System.Xml.Linq;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace GrpcWizardLibrary;

public static class GrpcWizard
{
    public static async Task<string> GenerateGrpcInfrastructure(Assembly callingAssembly, string nameSpace, string outputFolder)
    {
        string modelsNameSpace = $"{nameSpace}.Shared.Models";

        // serviceModels has a list of services and associated models
        List<ServiceModel> serviceModels = new List<ServiceModel>();

        List<string> protoMessageNames = new List<string>();

        var types = callingAssembly.GetTypes();
        if (types.Length == 0)
        {
            return "Assembly has no types";
        }
        else
        {
            foreach (var type in types)
            {
                var attributes = type.GetCustomAttributes(typeof(GrpcServiceAttribute), false);
                foreach (GrpcServiceAttribute attr in attributes)
                {
                    if (!type.IsInterface)
                    {
                        foreach (var attrType in attr.Types)
                        {
                            var serviceModel = (from x in serviceModels
                                                where x.ServiceType.Name == type.Name
                                                select x).FirstOrDefault();
                            if (serviceModel == null)
                            {
                                serviceModel = new ServiceModel()
                                {
                                    ServiceType = type,
                                };
                                serviceModels.Add(serviceModel);
                            }
                            serviceModel.ModelTypes.Add(attrType);
                        }
                    }
                }
            }

            if (serviceModels.Count == 0)
            {
                return "Service classes must have the [GrpcService] attribute";
            }
        }

        // Get the service names
        var serviceNames = new List<string>();

        // Make sure output folder exists
        EnsureFolderExists(outputFolder);

        // Create subfolders
        var ClientOutputFolder = $"{outputFolder}\\Client\\";
        var ClientServicesOutputFolder = $"{outputFolder}\\Client\\GrpcServices\\";
        var ServerOutputFolder = $"{outputFolder}\\Server\\";
        var ServerServicesOutputFolder = $"{outputFolder}\\Server\\GrpcServices\\";
        var SharedOutputFolder = $"{outputFolder}\\Shared\\";
        var SharedModelsOutputFolder = $"{outputFolder}\\Shared\\GrpcModels\\";
        var SharedConvertersOutputFolder = $"{outputFolder}\\Shared\\GrpcConverters\\";
        EnsureFolderExists(ClientOutputFolder);
        EnsureFolderExists(ClientServicesOutputFolder);
        DeleteExistingFilesInFolder(ClientServicesOutputFolder);
        EnsureFolderExists(ServerOutputFolder);
        EnsureFolderExists(ServerServicesOutputFolder);
        DeleteExistingFilesInFolder(ServerServicesOutputFolder);
        EnsureFolderExists(SharedOutputFolder);
        EnsureFolderExists(SharedModelsOutputFolder);
        DeleteExistingFilesInFolder(SharedModelsOutputFolder);
        EnsureFolderExists(SharedConvertersOutputFolder);
        DeleteExistingFilesInFolder(SharedConvertersOutputFolder);

        // start building the proto file string
        var protoSb = new StringBuilder();
        protoSb.AppendLine("syntax = \"proto3\";");
        protoSb.AppendLine($"option csharp_namespace = \"{modelsNameSpace}\";");
        protoSb.AppendLine("");
        string protoFileName = "grpc.proto";

        foreach (var sm in serviceModels)
        {
            var serviceType = sm.ServiceType;
            var serviceName = serviceType.Name;

            if (!serviceName.EndsWith("Service"))
            {
                return "GrpcService names must end with the word 'Service'";
            }
            else
            {
                serviceName = serviceName.Substring(0, serviceName.Length - 7);
            }
            serviceNames.Add(serviceName);

            // load the interface
            var interfaces = serviceType.GetInterfaces();
            Type serviceInterface = null;
            if (interfaces.Count() == 0)
            {
                return "Can not find an interface with the [GrpcService] attribute";
            }
            else
            {
                serviceInterface = interfaces[0];
            }

            protoSb.AppendLine($"service Grpc_{serviceName} " + "{");

            // Create a list of types used in this service
            var messageTypes = new List<Type>();
            var methods = serviceInterface.GetMethods();

            // Add the models
            foreach (var modelType in sm.ModelTypes)
            {
                var match = (from x in messageTypes
                             where x.Name == modelType.Name
                             select x).FirstOrDefault();
                if (match == null)
                    messageTypes.Add(modelType);
            }

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters == null || parameters.Length == 0)
                {
                    return $"Service method {method.Name} requires one input parameter";
                }
                if (parameters.Length > 1)
                {
                    return $"Service method {method.Name} has more than one parameter";
                }
                var inputParam = parameters[0];
                if (method.ReturnParameter.ParameterType.Name != "Task`1")
                {
                    return "Service methods must return a Task<>";
                }
                var returnType = method.ReturnParameter.ParameterType.GenericTypeArguments[0].Name;

                string newline = $"    rpc {method.Name} (Grpc_{inputParam.ParameterType.Name}) returns (Grpc_{returnType});";

                // Add parameter types
                if (!messageTypes.Contains(inputParam.ParameterType))
                {
                    messageTypes.Add(inputParam.ParameterType);
                }
                if (!messageTypes.Contains(method.ReturnParameter.ParameterType.GenericTypeArguments[0]))
                {
                    messageTypes.Add(method.ReturnParameter.ParameterType.GenericTypeArguments[0]);
                }

                protoSb.AppendLine(newline);
            }
            protoSb.AppendLine("}");

            // loop through each message type in this service
            foreach (Type t in messageTypes)
            {
                var match = (from x in messageTypes
                             where protoMessageNames.Contains(t.Name)
                             select x).FirstOrDefault();
                if (match == null)
                {
                    protoMessageNames.Add(t.Name);

                    protoSb.AppendLine("");
                    protoSb.AppendLine($"message Grpc_{t.Name} " + "{");
                    var props = t.GetProperties();
                    int ordinal = 1;
                    foreach (var prop in props)
                    {
                        string propertyType = "";

                        if (prop.PropertyType.Name == "Int32")
                        {
                            propertyType = "int32";
                        }
                        else if (prop.PropertyType.Name == "Int64")
                        {
                            propertyType = "int64";
                        }
                        else if (prop.PropertyType.Name == "UInt32")
                        {
                            propertyType = "uint32";
                        }
                        else if (prop.PropertyType.Name == "UInt64")
                        {
                            propertyType = "uint64";
                        }
                        else if (prop.PropertyType.Name == "Boolean")
                        {
                            propertyType = "bool";
                        }
                        else if (prop.PropertyType.Name == "Single")
                        {
                            propertyType = "float";
                        }
                        else if (prop.PropertyType.Name == "Double")
                        {
                            propertyType = "double";
                        }
                        else if (prop.PropertyType.Name == "Decimal")
                        {
                            propertyType = "double";
                        }
                        else if (prop.PropertyType.Name == "String")
                        {
                            propertyType = "string";
                        }
                        else if (prop.PropertyType.Name == "DateTime")
                        {
                            propertyType = "int64";
                        }
                        else if (prop.PropertyType.Name == "Byte[]")
                        {
                            // byte array translates to bytes
                            propertyType = "bytes";
                        }
                        else if (prop.PropertyType.Name.EndsWith("[]"))
                        {
                            // this is an array
                            var listType = prop.PropertyType.Name.Substring(0, prop.PropertyType.Name.Length - 2);

                            if (listType == "Int32")
                                propertyType = "repeated int32";
                            else if (listType == "Int64")
                                propertyType = "repeated int64";
                            else if (listType == "UInt32")
                                propertyType = "repeated uint32";
                            else if (listType == "UInt64")
                                propertyType = "repeated uint64";
                            else if (listType == "Boolean")
                                propertyType = "repeated bool";
                            else if (listType == "String")
                                propertyType = "repeated string";
                            else if (listType == "DateTime")
                                propertyType = "repeated int64";    // Dates are converted to long values
                            else if (listType == "Single")
                                propertyType = "repeated float";
                            else if (listType == "Double")
                                propertyType = "repeated double";
                            else if (listType == "Decimal")
                                propertyType = "repeated double";
                            else if (listType == "Byte[]")
                                propertyType = "repeated bytes";
                            else
                                propertyType = "repeated Grpc_" + listType;

                        }
                        else if (prop.PropertyType.Name == "List`1")
                        {
                            // this is a List<T>

                            var listType = prop.PropertyType.GenericTypeArguments[0].Name;

                            if (listType == "Int32")
                                propertyType = "repeated int32";
                            else if (listType == "Int64")
                                propertyType = "repeated int64";
                            else if (listType == "UInt32")
                                propertyType = "repeated uint32";
                            else if (listType == "UInt64")
                                propertyType = "repeated uint64";
                            else if (listType == "Boolean")
                                propertyType = "repeated bool";
                            else if (listType == "String")
                                propertyType = "repeated string";
                            else if (listType == "DateTime")
                                propertyType = "repeated int64";    // Dates are converted to long values
                            else if (listType == "Single")
                                propertyType = "repeated float";
                            else if (listType == "Double")
                                propertyType = "repeated double";
                            else if (listType == "Decimal")
                                propertyType = "repeated double";
                            else if (listType == "Byte[]")
                                propertyType = "repeated bytes";
                            else
                                propertyType = "repeated Grpc_" + listType;
                        }
                        else
                        {
                            // This is one of our classes
                            propertyType = $"Grpc_{prop.PropertyType.Name}";
                        }

                        // DateTime?
                        if (prop.PropertyType.Name == "DateTime")
                        {
                            protoSb.AppendLine($"    {propertyType} dt_{prop.Name} = {ordinal};");
                        }
                        else
                        {
                            protoSb.AppendLine($"    {propertyType} {CamelCase(prop.Name)} = {ordinal};");
                        }

                        ordinal++;
                    }
                    protoSb.AppendLine("}");
                }
            }
            
            Console.WriteLine(protoSb.ToString());

            // create a list of converter file names
            var converterFiles = new List<string>();

            // build converters
            string modelName = "";

            foreach (Type t in messageTypes)
            {
                var converterSb = new StringBuilder();
                string className = $"{t.Name}Converter";
                
                if (t.IsDefined(typeof(GrpcModelAttribute), false))
                {
                    modelName = t.Name;
                }

                converterSb.AppendLine("using Google.Protobuf;");
                converterSb.AppendLine($"using {modelsNameSpace};");
                converterSb.AppendLine("");
                converterSb.AppendLine($"namespace {modelsNameSpace};");
                converterSb.AppendLine($"    public static class {className}");
                converterSb.AppendLine("    {");
                converterSb.AppendLine($"        public static List<Grpc_{t.Name}> From{t.Name}List(List<{t.Name}> list)");
                converterSb.AppendLine("        {");
                converterSb.AppendLine($"            var result = new List<Grpc_{t.Name}>()" + ";");
                converterSb.AppendLine("            foreach (var item in list)");
                converterSb.AppendLine("            {");
                converterSb.AppendLine($"                result.Add(From{t.Name}(item))" + ";");
                converterSb.AppendLine("            }");
                converterSb.AppendLine("            return result;");
                converterSb.AppendLine("        }");
                converterSb.AppendLine("");
                converterSb.AppendLine($"        public static List<{t.Name}> FromGrpc_{t.Name}List(List<Grpc_{t.Name}> list)");
                converterSb.AppendLine("        {");
                converterSb.AppendLine($"            var result = new List<{t.Name}>()" + ";");
                converterSb.AppendLine("            foreach (var item in list)");
                converterSb.AppendLine("            {");
                converterSb.AppendLine($"                result.Add(FromGrpc_{t.Name}(item))" + ";");
                converterSb.AppendLine("            }");
                converterSb.AppendLine("            return result;");
                converterSb.AppendLine("        }");
                converterSb.AppendLine("");
                converterSb.AppendLine($"        public static Grpc_{t.Name} From{t.Name}({t.Name} item)");
                converterSb.AppendLine("        {");
                converterSb.AppendLine($"            var result = new Grpc_{t.Name}();");


                var props = t.GetProperties();
                foreach (var prop in props)
                {
                    if (prop.PropertyType.Name == "List`1")
                    {
                        var listType = prop.PropertyType.GenericTypeArguments[0].Name;
                        if (listType == "Int32" || listType == "Int64" || listType == "UInt32"
                            || listType == "UInt64" || listType == "Boolean" || listType == "Single"
                            || listType == "Double")
                        {
                            converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                            converterSb.AppendLine($"                result.{prop.Name}.AddRange(item.{prop.Name});");
                        }
                        else if (listType == "String")
                        {
                            converterSb.AppendLine($"            if (!string.IsNullOrWhiteSpace(item.{prop.Name})");
                            converterSb.AppendLine($"                result.{prop.Name}.AddRange(item.{prop.Name});");
                        }
                        else
                        {
                            converterSb.AppendLine($"            var {prop.Name.ToLower()} = {listType}Converter.From{listType}List(item.{prop.Name}.ToList());");
                            converterSb.AppendLine($"            result.{prop.Name}.AddRange({prop.Name.ToLower()});");
                        }

                    }
                    else if (prop.PropertyType.Name == "Byte[]")
                    {
                        converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                        converterSb.AppendLine($"                result.{prop.Name} = ByteString.CopyFrom(item.{prop.Name});");
                    }
                    else if (prop.PropertyType.Name.EndsWith("[]"))
                    {
                        var listType = prop.PropertyType.Name.Substring(0, prop.PropertyType.Name.Length - 2);
                        converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                        converterSb.AppendLine($"                result.{prop.Name}.AddRange(item.{prop.Name});");
                    }
                    else
                    {
                        if (prop.PropertyType.Name == "DateTime")
                        {
                            converterSb.AppendLine($"            result.Dt{prop.Name} = item.{prop.Name}.ToBinary();");
                        }
                        else if (prop.PropertyType.Name == "Decimal")
                        {
                            converterSb.AppendLine($"            result.{prop.Name} = (double)item.{prop.Name};");
                        }
                        else if (prop.PropertyType.Name == modelName)
                        {
                            converterSb.AppendLine($"            result.{prop.Name} = {modelName}Converter.From{modelName}(item.{prop.Name});");
                        }
                        else
                        {
                            converterSb.AppendLine($"            result.{prop.Name} = item.{prop.Name};");
                        }
                    }
                }

                converterSb.AppendLine("            return result;");
                converterSb.AppendLine("        }");
                converterSb.AppendLine("");
                converterSb.AppendLine("");
                converterSb.AppendLine($"        public static {t.Name} FromGrpc_{t.Name}(Grpc_{t.Name} item)");
                converterSb.AppendLine("        {");
                converterSb.AppendLine($"            var result = new {t.Name}();");

                props = t.GetProperties();
                foreach (var prop in props)
                {
                    if (prop.PropertyType.Name == "List`1")
                    {
                        var listType = prop.PropertyType.GenericTypeArguments[0].Name;
                        // is this a model?
                        if (t.IsDefined(typeof(GrpcModelAttribute), false))
                        {
                            // This is the Model class. Don't use a converter.
                            converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                            converterSb.AppendLine($"                result.{prop.Name} = item.{prop.Name}.ToList();");
                        }
                        else
                        {
                            converterSb.AppendLine($"            var {prop.Name.ToLower()} = {listType}Converter.FromGrpc_{listType}List(item.{prop.Name}.ToList());");
                            converterSb.AppendLine($"            result.{prop.Name}.AddRange({prop.Name.ToLower()});");
                        }
                    }
                    else
                    {
                        if (prop.PropertyType.Name == "DateTime")
                        {
                            converterSb.AppendLine($"            result.{prop.Name} = System.DateTime.FromBinary(item.Dt{prop.Name});");
                        }
                        else if (prop.PropertyType.Name == "Byte[]")
                        {
                            converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                            converterSb.AppendLine($"                result.{prop.Name} = item.{prop.Name}.ToByteArray();");
                        }
                        else if (prop.PropertyType.Name.EndsWith("[]"))
                        {
                            converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                            converterSb.AppendLine($"                result.{prop.Name} = item.{prop.Name}.ToArray();");
                        }
                        else if (prop.PropertyType.Name == "Decimal")
                        {
                            converterSb.AppendLine($"            result.{prop.Name} = (decimal)item.{prop.Name};");
                        }
                        else
                        {
                            if (prop.PropertyType.Name == modelName)
                            {
                                converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                                converterSb.AppendLine($"                result.{prop.Name} = {modelName}Converter.FromGrpc_{modelName}(item.{prop.Name});");
                            }
                            else if (!prop.PropertyType.IsValueType)
                            {
                                converterSb.AppendLine($"            if (item.{prop.Name} != null)");
                                converterSb.AppendLine($"                result.{prop.Name} = item.{prop.Name};");
                            }
                            else // value types don't need a null check
                            {
                                converterSb.AppendLine($"            result.{prop.Name} = item.{prop.Name};");
                            }
                        }
                    }
                }

                converterSb.AppendLine("            return result;");
                converterSb.AppendLine("        }");
                converterSb.AppendLine("");
                converterSb.AppendLine("    }");
                Console.WriteLine();
                Console.WriteLine(converterSb.ToString());

                string converterFileName = $"{SharedConvertersOutputFolder}\\{className}.cs";
                converterFiles.Add($"{className}.cs");
                File.WriteAllText(converterFileName, converterSb.ToString());

            }

            // build the Service file
            var serviceSb = new StringBuilder();

            serviceSb.AppendLine("using Grpc.Core;");
            serviceSb.AppendLine("using Google.Protobuf.WellKnownTypes;");
            serviceSb.AppendLine($"using {modelsNameSpace};");
            serviceSb.AppendLine("");
            serviceSb.AppendLine($"namespace {nameSpace};");
            serviceSb.AppendLine($"    public class Grpc_{serviceName}Service : Grpc_{serviceName}.Grpc_{serviceName}Base");
            serviceSb.AppendLine("    {");
            serviceSb.AppendLine($"        {serviceType.Name} {CamelCase(serviceType.Name)};");
            serviceSb.AppendLine("");
            serviceSb.AppendLine($"        public Grpc_{serviceName}Service({serviceType.Name} _{CamelCase(serviceType.Name)})");
            serviceSb.AppendLine("        {");
            serviceSb.AppendLine($"            {CamelCase(serviceType.Name)} = _{CamelCase(serviceType.Name)};");
            serviceSb.AppendLine("        }");

            // original interface methods
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                var inputParam = parameters[0];
                var returnType = method.ReturnParameter.ParameterType.GenericTypeArguments[0].Name;

                serviceSb.AppendLine($"        public override async Task<Grpc_{returnType}> {method.Name}(Grpc_{inputParam.ParameterType.Name} request, ServerCallContext context)");
                serviceSb.AppendLine("        {");
                serviceSb.AppendLine($"            var baseRequest = {inputParam.ParameterType.Name}Converter.FromGrpc_{inputParam.ParameterType.Name}(request);");
                serviceSb.AppendLine($"            var baseResponse = await {CamelCase(serviceType.Name)}.{method.Name}(baseRequest)" + ";");
                serviceSb.AppendLine($"            var response = {returnType}Converter.From{returnType}(baseResponse);");
                serviceSb.AppendLine("            return response;");
                serviceSb.AppendLine("        }");
                serviceSb.AppendLine("");
            }

            serviceSb.AppendLine("    }");

            Console.WriteLine();
            Console.WriteLine(serviceSb.ToString());

            // Write the client service
            var clientServiceSb = new StringBuilder();
            clientServiceSb.AppendLine($"using {modelsNameSpace};");
            clientServiceSb.AppendLine("using System.Threading.Tasks;");
            clientServiceSb.AppendLine("");
            clientServiceSb.AppendLine($"public class {serviceName}Client");
            clientServiceSb.AppendLine("{");
            clientServiceSb.AppendLine($"    Grpc_{serviceName}.Grpc_{serviceName}Client grpc_{serviceName}Client;");
            clientServiceSb.AppendLine($"    public {serviceName}Client(Grpc_{serviceName}.Grpc_{serviceName}Client _grpc_{serviceName}Client)");
            clientServiceSb.AppendLine("    {");
            clientServiceSb.AppendLine($"        grpc_{serviceName}Client = _grpc_{serviceName}Client;");
            clientServiceSb.AppendLine("    }");
            clientServiceSb.AppendLine("");

            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                var inputParam = parameters[0];
                var returnType = method.ReturnParameter.ParameterType.GenericTypeArguments[0].Name;

                clientServiceSb.AppendLine($"    public async Task<{returnType}> {method.Name}Async({inputParam.ParameterType.Name} request)");
                clientServiceSb.AppendLine("    {");
                clientServiceSb.AppendLine($"        var {CamelCase(inputParam.ParameterType.Name)} = {inputParam.ParameterType.Name}Converter.From{inputParam.ParameterType.Name}(request);");
                clientServiceSb.AppendLine($"        var result = await grpc_{serviceName}Client.{method.Name}Async({CamelCase(inputParam.ParameterType.Name)});");
                clientServiceSb.AppendLine($"        return {returnType}Converter.FromGrpc_{returnType}(result);");
                clientServiceSb.AppendLine("    }");
                clientServiceSb.AppendLine("");
            }
            clientServiceSb.AppendLine("}");

            Console.WriteLine();
            Console.WriteLine(clientServiceSb.ToString());

            var serviceFileName = $"{ServerServicesOutputFolder}\\Grpc_{serviceName}Service.cs";
            File.WriteAllText(serviceFileName, serviceSb.ToString());

            var clientServiceFileName = $"{ClientServicesOutputFolder}\\{serviceName}Client.cs";
            File.WriteAllText(clientServiceFileName, clientServiceSb.ToString());

        }

        // Get NuGet Versions
        string verGoogleProtobuf = await GetLatestNugetVersion("Google.Protobuf");
        string verGrpcNetClient = await GetLatestNugetVersion("Grpc.Net.Client");
        string verGrpcTools = await GetLatestNugetVersion("Grpc.Tools");
        string verGrpcAspNetCore = await GetLatestNugetVersion("Grpc.AspNetCore");
        string verGrpcAspNetCoreWeb = await GetLatestNugetVersion("Grpc.AspNetCore.Web");

        var sharedSb = new StringBuilder();
        sharedSb.AppendLine("    <ItemGroup>");
        sharedSb.AppendLine($"        <PackageReference Include=\"Google.Protobuf\" Version=\"{verGoogleProtobuf}\" />");
        sharedSb.AppendLine($"        <PackageReference Include=\"Grpc.Net.Client\" Version=\"{verGrpcNetClient}\" />");
        sharedSb.AppendLine($"        <PackageReference Include=\"Grpc.Tools\" Version=\"{verGrpcTools}\" >");
        sharedSb.AppendLine("            <PrivateAssets>all</PrivateAssets>");
        sharedSb.AppendLine("            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>");
        sharedSb.AppendLine("        </PackageReference>");
        sharedSb.AppendLine("    </ItemGroup>");
        sharedSb.AppendLine("    <ItemGroup>");
        sharedSb.AppendLine("        <SupportedPlatform Include=\"browser\" />");
        sharedSb.AppendLine($"        <Protobuf Include=\"{protoFileName}\" />");
        sharedSb.AppendLine("    </ItemGroup>");

        Console.WriteLine();
        Console.WriteLine("shared.csproj");
        Console.WriteLine(sharedSb.ToString());

        var serverSb = new StringBuilder();
        serverSb.AppendLine("    <ItemGroup>");
        serverSb.AppendLine($"        <PackageReference Include=\"Grpc.AspNetCore\" Version=\"{verGrpcAspNetCore}\" />");
        serverSb.AppendLine($"        <PackageReference Include=\"Grpc.AspNetCore.Web\" Version=\"{verGrpcAspNetCoreWeb}\" />");
        serverSb.AppendLine("    </ItemGroup>");

        Console.WriteLine();
        Console.WriteLine("server.csproj");
        Console.WriteLine(serverSb.ToString());

        string verGrpcNetClientWeb = await GetLatestNugetVersion("Grpc.Net.Client.Web");

        var clientSb = new StringBuilder();
        clientSb.AppendLine("    <ItemGroup>");
        clientSb.AppendLine($"        <PackageReference Include=\"Grpc.Net.Client.Web\" Version=\"{verGrpcNetClientWeb}\" />");
        clientSb.AppendLine("    </ItemGroup>");

        Console.WriteLine();
        Console.WriteLine("client.csproj");
        Console.WriteLine(clientSb.ToString());

        var serverProgramSb = new StringBuilder();
        serverProgramSb.AppendLine("// Add the following after var builder = WebApplication.CreateBuilder(args);");
        serverProgramSb.AppendLine("builder.Services.AddGrpc();");
        foreach (var thisServiceName in serviceNames)
        {
            serverProgramSb.AppendLine($"builder.Services.AddSingleton<{thisServiceName}Service>();");
        }
        serverProgramSb.AppendLine("");
        serverProgramSb.AppendLine("// Add the following after app.UseRouting()");
        serverProgramSb.AppendLine("app.UseGrpcWeb();");
        foreach (var thisServiceName in serviceNames)
        {
            serverProgramSb.AppendLine($"app.MapGrpcService<Grpc_{thisServiceName}Service>().EnableGrpcWeb();");
        }
        serverProgramSb.AppendLine("");

        Console.WriteLine();
        Console.WriteLine("Program.cs:");
        Console.WriteLine(serverProgramSb.ToString());

        var programSb = new StringBuilder();
        foreach (var thisServiceName in serviceNames)
        {
            programSb.AppendLine("builder.Services.AddSingleton(services =>");
            programSb.AppendLine("{");
            programSb.AppendLine("    var httpClient = new HttpClient(new GrpcWebHandler(GrpcWebMode.GrpcWeb, new HttpClientHandler()));");
            programSb.AppendLine("    var baseUri = builder.HostEnvironment.BaseAddress;");
            programSb.AppendLine("    var channel = GrpcChannel.ForAddress(baseUri, new GrpcChannelOptions { HttpClient = httpClient });");
            programSb.AppendLine($"    return new Grpc_{thisServiceName}.Grpc_{thisServiceName}Client(channel);");
            programSb.AppendLine("});");
            programSb.AppendLine("");
        }

        foreach (var thisServiceName in serviceNames)
        {
            programSb.AppendLine($"builder.Services.AddScoped<{thisServiceName}Client>();");
        }
        Console.WriteLine();
        Console.WriteLine("Program.cs:");
        Console.WriteLine(programSb.ToString());

        // Sample Index page
        var indexSb = new StringBuilder();
        indexSb.AppendLine("@page \"/\"");
        indexSb.AppendLine($"@inject PeopleClient PeopleClient");
        indexSb.AppendLine("<PageTitle>Index</PageTitle>");
        indexSb.AppendLine("");
        indexSb.AppendLine("<button class=\"btn btn-primary\" @onclick=\"GetAllButton_Clicked\">Get All</button>");
        indexSb.AppendLine("<br/>");
        indexSb.AppendLine("<br/>");
        indexSb.AppendLine("");
        indexSb.AppendLine($"@if (People.Count > 0)");
        indexSb.AppendLine("{");
        indexSb.AppendLine("    <ul>");
        indexSb.AppendLine($"        @foreach (var item in People)");
        indexSb.AppendLine("        {");
        indexSb.AppendLine("            <li><span>(@item.Id) @item.FirstName @item.LastName</span></li>");
        indexSb.AppendLine("        }");
        indexSb.AppendLine("    </ul>");
        indexSb.AppendLine("}");
        indexSb.AppendLine("");
        indexSb.AppendLine("");

        indexSb.AppendLine("@code{");
        indexSb.AppendLine("");
        indexSb.AppendLine("    List<Person> People = new List<Person>();");
        indexSb.AppendLine("");
        indexSb.AppendLine("    async Task GetAllButton_Clicked()");
        indexSb.AppendLine("    {");
        indexSb.AppendLine($"        People.Clear();");
        indexSb.AppendLine($"        var result = await PeopleClient.GetAllAsync(new GetAllPeopleRequest());");
        indexSb.AppendLine($"        People.AddRange(result.People);");
        indexSb.AppendLine("    }");
        indexSb.AppendLine("}");
        indexSb.AppendLine("");

        // Copy model classes to the Output Folder
        var modelsFolderName = $"{Environment.CurrentDirectory}\\Models";
        if (Directory.Exists(modelsFolderName))
        {
            var files = Directory.GetFiles(modelsFolderName, "*.cs");
            foreach (var file in files)
            {
                var fileNameOnly = Path.GetFileName(file);
                var TargetFileName = $"{SharedModelsOutputFolder}\\{fileNameOnly}";
                var lines = File.ReadAllLines(file);
                var modelsSb = new StringBuilder();
                foreach (var line in lines)
                {
                    if (line.StartsWith("using GrpcWizardLibrary;"))
                    {
                        // ignore using statement
                    }
                    else if (line.StartsWith("[GrpcModel]"))
                    {
                        // ignore GrpcModel attribute
                    }
                    else
                    {
                        //ok 
                        modelsSb.AppendLine(line);
                    }
                }
                File.WriteAllText(TargetFileName, modelsSb.ToString());
            }
        }

        // Copy service classes to the Output Folder
        var servicesFolderName = $"{Environment.CurrentDirectory}\\Services";
        if (Directory.Exists(servicesFolderName))
        {
            var files = Directory.GetFiles(servicesFolderName, "*.cs");
            foreach (var file in files)
            {
                var fileNameOnly = Path.GetFileName(file);
                var TargetFileName = $"{ServerServicesOutputFolder}\\{fileNameOnly}";
                var lines = File.ReadAllLines(file);
                var servicesSb = new StringBuilder();
                foreach (var line in lines)
                {
                    if (line.StartsWith("using GrpcWizardLibrary;"))
                    {
                        // ignore using statement
                    }
                    else if (line.StartsWith("[GrpcService"))
                    {
                        // ignore GrpcService attribute
                    }
                    else
                    {
                        //ok 
                        servicesSb.AppendLine(line);
                    }
                }
                File.WriteAllText(TargetFileName, servicesSb.ToString());
            }
        }

        // COMBINE DATA and WRITE FILES

        var readmeSb = new StringBuilder();
        readmeSb.AppendLine("Instructions for modifying your .NET 7 Hosted Blazor WebAssembly app to support gRPC");
        readmeSb.AppendLine();

        readmeSb.AppendLine("Shared Project:");
        readmeSb.AppendLine("===============");
        readmeSb.AppendLine("1) Add the following to the Shared project .csproj file:");
        readmeSb.AppendLine();
        readmeSb.Append(sharedSb);
        readmeSb.AppendLine();
        readmeSb.AppendLine($"2) Add all the generated files from the Shared folder to the Shared project.");
        readmeSb.AppendLine();
        readmeSb.AppendLine();

        readmeSb.AppendLine("Server Project:");
        readmeSb.AppendLine("===============");
        readmeSb.AppendLine("1) Add the following to the Server project .csproj file:");
        readmeSb.AppendLine();
        readmeSb.Append(serverSb);
        readmeSb.AppendLine("");
        readmeSb.AppendLine($"2) Add all the generated files from the Server folder to the Server project.");
        readmeSb.AppendLine();
        readmeSb.AppendLine("3) Add the following to the Server project Program.cs file:");
        readmeSb.AppendLine();
        readmeSb.Append(serverProgramSb);
        readmeSb.AppendLine();
        readmeSb.AppendLine();

        readmeSb.AppendLine("Client Project:");
        readmeSb.AppendLine("===============");
        readmeSb.AppendLine("1) Add the following to the Client project .csproj file:");
        readmeSb.AppendLine();
        readmeSb.Append(clientSb);
        readmeSb.AppendLine();
        readmeSb.AppendLine($"2) Add all the generated files from the Client folder to the Client project.");
        readmeSb.AppendLine();
        readmeSb.AppendLine("3) Add the following to the Client project Program.cs file before the line \"await builder.Build().RunAsync();\":");
        readmeSb.AppendLine();
        readmeSb.Append(programSb);
        readmeSb.AppendLine();
        readmeSb.AppendLine("4) Add the following @using statement to the Client project _Imports.razor file:");
        readmeSb.AppendLine($"     @using {modelsNameSpace}");
        readmeSb.AppendLine();
        readmeSb.AppendLine("Here's a sample Index.razor file:");
        readmeSb.Append(indexSb);
        readmeSb.AppendLine();

        string readmeFileName = $"{outputFolder}\\README.txt";
        File.WriteAllText(readmeFileName, readmeSb.ToString());

        string protoFullFileName = $"{SharedOutputFolder}\\{protoFileName}";
        File.WriteAllText(protoFullFileName, protoSb.ToString());


        return "OK";
    }

    static void EnsureFolderExists(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    static void DeleteExistingFilesInFolder(string path)
    {
        if (!Directory.Exists(path)) return;

        var files = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

    private static string CamelCase(string Text)
    {
        return Text.Substring(0, 1).ToLower() + Text.Substring(1);
    }

    static async Task<string> GetLatestNugetVersion(string PackageName)
    {
        ILogger logger = NullLogger.Instance;
        CancellationToken cancellationToken = CancellationToken.None;

        SourceCacheContext cache = new SourceCacheContext();
        SourceRepository repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        PackageMetadataResource resource = await repository.GetResourceAsync<PackageMetadataResource>();

        IEnumerable<IPackageSearchMetadata> packages = await resource.GetMetadataAsync(
            PackageName,
            includePrerelease: false,
            includeUnlisted: false,
            cache,
            logger,
            cancellationToken);

        return packages.Last().Identity.Version.ToString();
    }
}
