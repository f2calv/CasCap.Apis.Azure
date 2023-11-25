namespace CasCap.Tests;

public interface IAzQueueService : IAzQueueStorageBase
{
}

public class AzQueueService : AzQueueStorageBase, IAzQueueService
{
    public AzQueueService(ILogger<AzQueueService> logger, string connectionString)
        : base(logger, connectionString, queueName: "wibble")
    {
    }
}