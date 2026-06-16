namespace GorillazDiscordBot.Configuration;

public class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "gorillazbot";
}
