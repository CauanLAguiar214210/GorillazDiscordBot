using GorillazDiscordBot.Configuration;
using GorillazDiscordBot.Domain.Interfaces;
using GorillazDiscordBot.Entity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GorillazDiscordBot.Data.Repository;

public class GuildMemberRepository : MongoRepository<GuildMember>, IGuildMemberRepository
{
    private readonly ILogger<GuildMemberRepository> _logger;

    public GuildMemberRepository(IOptions<MongoOptions> options, ILogger<GuildMemberRepository> logger)
        : base(options)
    {
        _logger = logger;
    }

    public async Task<GuildMember?> GetAsync(ulong guildId, ulong userId)
    {
        var filter = Builders<GuildMember>.Filter.Eq(m => m.GuildId, guildId)
            & Builders<GuildMember>.Filter.Eq(m => m.UserId, userId);
        return await Collection.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<GuildMember>> GetAllAsync(ulong guildId)
    {
        var filter = Builders<GuildMember>.Filter.Eq(m => m.GuildId, guildId);
        return await Collection.Find(filter).ToListAsync();
    }

    public async Task<List<GuildMember>> GetManyAsync(ulong guildId, IEnumerable<ulong> userIds)
    {
        var filter = Builders<GuildMember>.Filter.Eq(m => m.GuildId, guildId)
            & Builders<GuildMember>.Filter.In(m => m.UserId, userIds);
        return await Collection.Find(filter).ToListAsync();
    }

    public async Task AddWarningAsync(ulong guildId, ulong userId, string username, UserWarning warning)
    {
        var member = await GetOrCreateAsync(guildId, userId, username);
        member.Warnings.Add(warning);
        await SaveAsync(member);
    }

    public async Task<bool> RemoveWarningAsync(ulong guildId, ulong userId, string warningId)
    {
        var member = await GetAsync(guildId, userId);
        if (member == null) return false;

        var removed = member.Warnings.RemoveAll(w => w.Id == warningId) > 0;
        if (!removed) return false;

        await SaveAsync(member);
        return true;
    }

    public async Task SetMuteAsync(ulong guildId, ulong userId, string username, DateTime? until)
    {
        var member = await GetOrCreateAsync(guildId, userId, username);
        member.MuteUntil = until;
        await SaveAsync(member);
    }

    public async Task SetBanAsync(ulong guildId, ulong userId, string username, bool isBanned)
    {
        var member = await GetOrCreateAsync(guildId, userId, username);
        member.IsBanned = isBanned;
        await SaveAsync(member);
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<GuildMember>(
                    Builders<GuildMember>.IndexKeys.Ascending(m => m.GuildId).Ascending(m => m.UserId),
                    new CreateIndexOptions { Unique = true, Name = "uq_GuildId_UserId" }),
                new CreateIndexModel<GuildMember>(
                    Builders<GuildMember>.IndexKeys.Ascending(m => m.UserId),
                    new CreateIndexOptions { Name = "idx_UserId" })
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao garantir índices da collection GuildMember");
        }
    }

    private async Task<GuildMember> GetOrCreateAsync(ulong guildId, ulong userId, string username)
    {
        var member = await GetAsync(guildId, userId);
        if (member != null) return member;

        member = new GuildMember
        {
            Id = ObjectId.GenerateNewId().ToString(),
            GuildId = guildId,
            UserId = userId,
            Username = username
        };

        await CreateAsync(member);
        return member;
    }

    private async Task SaveAsync(GuildMember member)
    {
        member.UpdatedAt = DateTime.UtcNow;

        try
        {
            var filter = Builders<GuildMember>.Filter.Eq(m => m.Id, member.Id);
            await Collection.ReplaceOneAsync(filter, member);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir moderação do usuário {userId} no servidor {guildId}",
                member.UserId, member.GuildId);
        }
    }
}