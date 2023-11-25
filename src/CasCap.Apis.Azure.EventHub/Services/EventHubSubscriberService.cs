using System.Collections.Concurrent;
namespace CasCap.Services;

public interface IEventHubSubscriberService<T>
{
    Task InitiateReceive(CancellationToken cancellationToken);
}

//https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/eventhub/Azure.Messaging.EventHubs/MigrationGuide.md
public abstract class EventHubSubscriberService<T> : IEventHubSubscriberService<T>
{
    readonly ILogger _logger;

    readonly string _eventHubName;
    readonly string _eventHubConnectionString;
    readonly string _storageConnectionString;
    readonly string _leaseContainerName;

    BlobContainerClient _checkpointStore;
    EventProcessorClient _eventProcessorClient;

    public EventHubSubscriberService(ILogger<EventHubSubscriberService<T>> logger,
        string eventHubName,
        string eventHubConnectionString,
        string storageConnectionString,
        string leaseContainerName)
    {
        _logger = logger;
        _eventHubName = eventHubName ?? throw new ArgumentException("required!", nameof(eventHubName));
        _eventHubConnectionString = eventHubConnectionString ?? throw new ArgumentException($"required!", nameof(_eventHubConnectionString));
        _storageConnectionString = storageConnectionString ?? throw new ArgumentException("required!", nameof(storageConnectionString));
        _leaseContainerName = leaseContainerName ?? throw new ArgumentException("required!", nameof(leaseContainerName));

        _checkpointStore = new BlobContainerClient(
            _storageConnectionString, blobContainerName: _leaseContainerName);

        _eventProcessorClient = new EventProcessorClient(
            _checkpointStore,
            EventHubConsumerClient.DefaultConsumerGroupName,
            _eventHubConnectionString,
            _eventHubName);
    }

    ConcurrentDictionary<string, int> partitionEventCount = new();

    public async Task InitiateReceive(CancellationToken cancellationToken)
    {

        try
        {
            await _checkpointStore.CreateIfNotExistsAsync();
            _eventProcessorClient.ProcessEventAsync += processEventHandler;
            _eventProcessorClient.ProcessErrorAsync += processErrorHandler;
            try
            {
                _logger.LogDebug("_eventProcessorClient.StartProcessingAsync... for {EventHubName}", _eventHubName);
                await _eventProcessorClient.StartProcessingAsync(cancellationToken);
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // This is expected if the cancellation token is signaled.
            }
            finally
            {
                // This may take up to the length of time defined as part of the configured TryTimeout of the processor; by default, this is 60 seconds.
                await _eventProcessorClient.StopProcessingAsync();
            }
        }
        catch
        {
            // The processor will automatically attempt to recover from any failures, either transient or fatal, and continue processing.
            // Errors in the processor's operation will be surfaced through its error handler.
            //
            // If this block is invoked, then something external to the processor was the source of the exception.
        }
        finally
        {
            // It is encouraged that you unregister your handlers when you have finished using the Event Processor to ensure proper cleanup.
            // This is especially important when using lambda expressions or handlers in any form that may contain closure scopes or hold other references.
            _eventProcessorClient.ProcessEventAsync -= processEventHandler;
            _eventProcessorClient.ProcessErrorAsync -= processErrorHandler;
        }
    }

    async Task processEventHandler(ProcessEventArgs args)
    {
        try
        {
            // If the cancellation token is signaled, then the processor has been asked to stop. It will invoke this handler with any events that were in flight;
            // these will not be lost if not processed.
            //
            // It is up to the handler to decide whether to take action to process the event or to cancel immediately.

            if (args.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            var partitionId = args.Partition.PartitionId;
            var bytes = args.Data.EventBody.ToArray();
            //_logger.LogDebug($"Event from partition { partitionId } with length { bytes.Length }.");
            if (bytes is not null)
            {
                var obj = bytes.FromMessagePack<T>();
                _logger.LogInformation("Message received. Partition: '{partitionId}', Data: '{obj}'", partitionId, obj);
            }
            else
                _logger.LogWarning($"Message received. Partition: '{partitionId}', Data: null", partitionId);

            var eventsSinceLastCheckpoint = partitionEventCount.AddOrUpdate(
                key: partitionId,
                addValue: 1,
                updateValueFactory: (_, currentCount) => currentCount + 1);

            if (eventsSinceLastCheckpoint >= 50)
            {
                await args.UpdateCheckpointAsync();
                partitionEventCount[partitionId] = 0;
            }
        }
        catch
        {
            // It is very important that you always guard against exceptions in your handler code; the processor does not have enough understanding of your code to determine the correct action to take.
            // Any exceptions from your handlers go uncaught by the processor and will NOT be redirected to the error handler.
        }
    }

    Task processErrorHandler(ProcessErrorEventArgs args)
    {
        try
        {
            _logger.LogDebug("Error in the EventProcessorClient");
            _logger.LogDebug($"\tOperation: {args.Operation}");
            _logger.LogDebug($"\tException: {args.Exception}");
            _logger.LogDebug("");
        }
        catch
        {
            // It is very important that you always guard against exceptions in your handler code; the processor does not have enough understanding of your code to determine the correct action to take.
            // Any exceptions from your handlers go uncaught by the processor and will NOT be handled in any way.
        }

        return Task.CompletedTask;
    }
}
