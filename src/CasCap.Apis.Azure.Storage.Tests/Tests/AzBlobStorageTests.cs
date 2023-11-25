namespace CasCap.Tests;

public class AzBlobStorageTests : TestBase
{

    public AzBlobStorageTests(ITestOutputHelper output) : base(output) { }

    static readonly byte[] fileBytes =
    {
           0x1E, 0x00, 0x00, 0x00, 0x0E, 0x04, 0x47, 0x00, 0x00, 0x00, 0x00, 0x00,
           0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0xFF, 0x18, 0x0B, 0x0E, 0xFF,
           0x12, 0x03, 0x00, 0x00, 0x0E, 0x6D, 0x15, 0x34, 0x15, 0x20, 0x12, 0x10,
           0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xEC, 0x16, 0x1F, 0x00,
           0x00, 0x00, 0x04, 0x29, 0x92, 0x11, 0x00, 0x00, 0x04, 0xA9, 0x0B, 0x16,
           0x00, 0x00, 0x00, 0xB7, 0x16, 0xC1, 0x80, 0x40, 0xFD, 0x1B, 0x01, 0x8E,
           0x00, 0x00, 0x81, 0x40, 0xFD, 0x1A, 0x00, 0x1F, 0x00, 0x00, 0x00, 0x00,
           0x21, 0x00, 0x00, 0x00, 0xCE, 0x00, 0xED, 0xEB, 0x15, 0x00, 0x00, 0x00,
           0xCE, 0x40, 0x84, 0x15, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xCE, 0x80,
           0x15, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x0F, 0x80, 0x40, 0xFD, 0x61,
           0x04, 0x68, 0x4F, 0x4F, 0x68, 0x08, 0x00, 0x72, 0x72, 0x16, 0x41, 0x00
        };

    [Fact]
    public async Task TestVanillaFunctionality()
    {
        // Create a BlobServiceClient object which will be used to create a container client
        var blobServiceClient = new BlobServiceClient(_connectionString);

        //Create a unique name for the container
        //var containerName = $"quickstartblobs{Guid.NewGuid()}";
        var containerName = $"wibble2";

        await foreach (var container in blobServiceClient.GetBlobContainersAsync())
            Debug.WriteLine($"{container.Name} ({container.Properties.PublicAccess})");

        await foreach (var container in blobServiceClient.GetBlobContainersAsync(prefix: containerName))
            Debug.WriteLine($"{container.Name} ({container.Properties.PublicAccess})");

        /*
        //if we *know* the container already exists, generate the container client
        _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
        // if we *know* the container doesnt exist, create the container and return a container client
        var test = await blobServiceClient.CreateBlobContainerAsync(containerName);
        _containerClient = test.Value;
        */
        //if we *don't know* if the container already exists, generate the container client and attempt a create
        var containerClient = new BlobContainerClient(_connectionString, containerName);
        _ = await containerClient.CreateIfNotExistsAsync();

        await foreach (var item in containerClient.GetBlobsByHierarchyAsync())
        {
            Debug.WriteLine(item.Blob.Name);
        }

        // Get a reference to a blob
        var filename = $"subfolder/test{Guid.NewGuid()}.bin";
        var _blobClient = containerClient.GetBlobClient(filename);

        Debug.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", _blobClient.Uri);

        // Open the file and upload its data
        //using FileStream uploadFileStream = File.OpenRead(localFilePath);

        using (var stream = new MemoryStream(fileBytes, writable: false))
        {
            var res = await _blobClient.UploadAsync(stream, true);
            stream.Close();
        }

        // List all blobs in the container
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            Debug.WriteLine("\t" + blobItem.Name);
        }

        var downloadFilePath = Path.Combine("c:/temp", filename);
        Console.WriteLine("\nDownloading blob to\n\t{0}\n", downloadFilePath);
        // Download the blob's contents and save it to a file
        var downloadInfo = await _blobClient.DownloadAsync();

        var dir = Path.GetDirectoryName(downloadFilePath);
        if (dir is not null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        using (var fs = File.OpenWrite(downloadFilePath))
        {
            await downloadInfo.Value.Content.CopyToAsync(fs);
            fs.Close();
        }
        Assert.True(true);
    }

    [Fact]
    public async Task TestBlobService()
    {
        var blobName = $"{Guid.NewGuid()}.bin";

        await _blobSvc.CreateContainerIfNotExists("wibble");

        await _blobSvc.UploadBlob($"{Guid.NewGuid()}.bin", fileBytes);

        await _blobSvc.UploadBlob(blobName, fileBytes);

        var downloadedBytes = await _blobSvc.DownloadBlobAsync(blobName);
        Assert.NotNull(downloadedBytes);
    }
}