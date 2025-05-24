using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GorillazDiscordBot.Entity
{
    public class DiscordUserProfile
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("UserId")]
        public ulong UserId { get; set; }

        [BsonElement("Username")]
        public string Username { get; set; }

        [BsonElement("Points")]
        public int Points { get; set; }
    }
}
