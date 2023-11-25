namespace CasCap.Tests;

public interface IAzBlobService : IAzBlobStorageBase
{
}

public class AzBlobService : AzBlobStorageBase, IAzBlobService
{
    public AzBlobService(ILogger<AzBlobService> logger, string connectionString)
        : base(logger, connectionString, containerName: "wibble")
    {
    }
}