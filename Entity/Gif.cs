using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GorillazDiscordBot.Entity;

public class Gif
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("nome")]
    public string Nome { get; set; } = string.Empty;

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    [BsonElement("categoria")]
    public string Categoria { get; set; } = "geral";

    [BsonElement("addedBy")]
    public ulong AddedBy { get; set; }

    [BsonElement("addedAt")]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
