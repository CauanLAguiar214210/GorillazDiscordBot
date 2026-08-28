using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class UserRepository : MongoRepository<DiscordUserProfile>, IUserRepository
{
    public UserRepository(IOptions<MongoOptions> options) : base(options) { }

    public async Task<DiscordUserProfile> GetOrCreateAsync(ulong userId, string username)
    {
        var filter = Builders<DiscordUserProfile>.Filter.Eq(u => u.UserId, userId);
        var user = await Collection.Find(filter).FirstOrDefaultAsync();

        if (user != null)
        {
            if (user.Username != username)
            {
                var update = Builders<DiscordUserProfile>.Update.Set(u => u.Username, username);
                await Collection.UpdateOneAsync(filter, update);
            }
            return user;
        }

        var newUser = new DiscordUserProfile
        {
            UserId = userId,
            Username = username
        };

        await CreateAsync(newUser);
        return newUser;
    }
}