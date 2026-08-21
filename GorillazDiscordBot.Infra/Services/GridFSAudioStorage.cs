using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Infra.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace GorillazDiscordBot.Data.Services;

public class GridFSAudioStorage : IAudioFileStorage
{
    private readonly GridFSBucket _bucket;

    public GridFSAudioStorage(IOptions<MongoOptions> options)
        : this(CreateBucket(options))
    {
    }

    protected GridFSAudioStorage(GridFSBucket bucket)
    {
        _bucket = bucket;
    }

    private static GridFSBucket CreateBucket(IOptions<MongoOptions> options)
    {
        MongoMappings.Register();
        var client = new MongoClient(options.Value.ConnectionString);
        var database = client.GetDatabase(options.Value.DatabaseName);
        return new GridFSBucket(database, new GridFSBucketOptions { BucketName = "audio" });
    }

    public async Task<string> SaveAsync(Stream audioStream, string fileName)
    {
        var id = await _bucket.UploadFromStreamAsync(fileName, audioStream);
        return id.ToString();
    }

    public async Task<Stream?> OpenReadAsync(string fileId)
    {
        if (!ObjectId.TryParse(fileId, out var objectId))
            return null;

        try
        {
            return await _bucket.OpenDownloadStreamAsync(objectId);
        }
        catch (GridFSFileNotFoundException)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string fileId)
    {
        if (!ObjectId.TryParse(fileId, out var objectId))
            return;

        try
        {
            await _bucket.DeleteAsync(objectId);
        }
        catch (GridFSFileNotFoundException)
        {
        }
    }
}
