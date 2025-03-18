namespace FluentSim;

public class Util
{
    public static void CheckForSerializer(ISerializer serializer)
    {
        if (serializer == null)
            throw new SimulatorException("No serializer has been provided, before using the serialization methods make sure to provide a serializer in the constructor of the simulator");
    }
}