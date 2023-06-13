namespace GrpcWizardLibrary;

public class GrpcServiceAttribute : Attribute 
{
    public Type[] Types { get; set; }

    public GrpcServiceAttribute(params Type[] types)
    {
        this.Types = types;
    }
}
