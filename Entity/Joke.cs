using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GorillazDiscordBot.Entity
{
    internal class Joke
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("text")]
        public string Texto { get; set; } = null!;

        [BsonElement("category")]
        public string Categoria { get; set; } = "geral";
    }
}
