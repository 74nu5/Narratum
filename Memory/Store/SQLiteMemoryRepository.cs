namespace Narratum.Memory.Store;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Narratum.Memory;
using Narratum.Memory.Store.Entities;

/// <summary>
/// SQLite implementation of IMemoryRepository.
/// Provides CRUD operations and querying for Memorandum entities using Entity Framework Core.
/// </summary>
public class SQLiteMemoryRepository : IMemoryRepository
{
    private readonly MemoryDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of SQLiteMemoryRepository.
    /// </summary>
    /// <param name="dbContext">The EF Core database context.</param>
    public SQLiteMemoryRepository(MemoryDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <summary>
    /// Retrieves a memorandum by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the memorandum (as Guid).</param>
    /// <returns>The memorandum if found; null otherwise.</returns>
    public async Task<Memorandum?> GetByIdAsync(Guid id)
    {
        var entity = await _dbContext.Memoria
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id.ToString() && !m.IsDeleted);

        return entity?.ToDomain();
    }

    /// <summary>
    /// Retrieves all memoranda for a specific story world.
    /// </summary>
    /// <param name="worldId">The world identifier.</param>
    /// <returns>List of memoranda for the world, ordered by creation date descending.</returns>
    public async Task<IReadOnlyList<Memorandum>> GetByWorldAsync(Guid worldId)
    {
        var entities = await _dbContext.Memoria
            .AsNoTracking()
            .Where(m => m.WorldId == worldId.ToString() && !m.IsDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.ToDomain()).ToList();
    }

    /// <summary>
    /// Retrieves all memoranda mentioning a specific title pattern.
    /// </summary>
    /// <param name="worldId">The world identifier.</param>
    /// <param name="titlePattern">The title pattern to search for.</param>
    /// <returns>List of memoranda matching the pattern, ordered by creation date descending.</returns>
    public async Task<IReadOnlyList<Memorandum>> GetByTitleAsync(Guid worldId, string titlePattern)
    {
        if (string.IsNullOrWhiteSpace(titlePattern))
            throw new ArgumentException("Title pattern cannot be null or empty.", nameof(titlePattern));

        var pattern = titlePattern.Trim();

        var entities = await _dbContext.Memoria
            .AsNoTracking()
            .Where(m => m.WorldId == worldId.ToString() && 
                        !m.IsDeleted &&
                        m.Title.Contains(pattern))
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.ToDomain()).ToList();
    }

    /// <summary>
    /// Saves a new memorandum to the database.
    /// </summary>
    /// <param name="memorandum">The memorandum to save.</param>
    public async Task SaveAsync(Memorandum memorandum)
    {
        if (memorandum == null) throw new ArgumentNullException(nameof(memorandum));

        var entity = new MemorandumEntity
        {
            Id = memorandum.Id.ToString(),
            WorldId = memorandum.WorldId.ToString(),
            Title = memorandum.Title,
            Description = memorandum.Description,
            CanonicalStatesData = JsonSerializer.Serialize(memorandum.CanonicalStates),
            ViolationsData = JsonSerializer.Serialize(memorandum.Violations),
            CreatedAt = memorandum.CreatedAt,
            LastUpdated = memorandum.LastUpdated,
            SerializedData = JsonSerializer.Serialize(memorandum),
            ContentHash = ComputeHash(memorandum),
            StoredAt = DateTime.UtcNow
        };

        await _dbContext.Memoria.AddAsync(entity);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Deletes a memorandum (soft delete).
    /// </summary>
    /// <param name="id">The unique identifier of the memorandum.</param>
    /// <returns>Success if deleted; failure if not found.</returns>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _dbContext.Memoria
            .FirstOrDefaultAsync(m => m.Id == id.ToString() && !m.IsDeleted);

        if (entity == null)
            return false;

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Queries memoranda using advanced filtering criteria.
    /// </summary>
    /// <param name="query">The query criteria.</param>
    /// <returns>List of memoranda matching the criteria.</returns>
    public async Task<IReadOnlyList<Memorandum>> QueryAsync(MemoryQuery query)
    {
        if (query == null) throw new ArgumentNullException(nameof(query));

        var dbQuery = _dbContext.Memoria
            .AsNoTracking()
            .Where(m => !m.IsDeleted);

        // Filter by world
        if (query.WorldId.HasValue)
        {
            var worldIdString = query.WorldId.Value.ToString();
            dbQuery = dbQuery.Where(m => m.WorldId == worldIdString);
        }

        // Filter by date range
        if (query.FromDate.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.CreatedAt >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.CreatedAt <= query.ToDate.Value);
        }

        // Filter by title
        if (!string.IsNullOrWhiteSpace(query.TitleFilter))
        {
            var pattern = query.TitleFilter.Trim();
            dbQuery = dbQuery.Where(m => m.Title.Contains(pattern));
        }

        var entities = await dbQuery
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        return entities.Select(e => e.ToDomain()).ToList();
    }

    /// <summary>
    /// Saves multiple memoranda in a single transaction.
    /// </summary>
    /// <param name="memoria">The list of memoranda to save.</param>
    public async Task SaveAsync(IReadOnlyList<Memorandum> memoria)
    {
        if (memoria == null) throw new ArgumentNullException(nameof(memoria));
        if (memoria.Count == 0) return;

        var entities = memoria.Select(m => new MemorandumEntity
        {
            Id = m.Id.ToString(),
            WorldId = m.WorldId.ToString(),
            Title = m.Title,
            Description = m.Description,
            CanonicalStatesData = JsonSerializer.Serialize(m.CanonicalStates),
            ViolationsData = JsonSerializer.Serialize(m.Violations),
            CreatedAt = m.CreatedAt,
            LastUpdated = m.LastUpdated,
            SerializedData = JsonSerializer.Serialize(m),
            ContentHash = ComputeHash(m),
            StoredAt = DateTime.UtcNow
        }).ToList();

        await _dbContext.Memoria.AddRangeAsync(entities);
        await _dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Computes a hash of the memorandum content for integrity verification.
    /// </summary>
    private static string ComputeHash(Memorandum memorandum)
    {
        var data = $"{memorandum.Title}|{memorandum.Description}|{memorandum.CreatedAt.Ticks}";
        using (var sha = System.Security.Cryptography.SHA256.Create())
        {
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}

/// <summary>
/// Extension methods for converting between domain and EF entities.
/// </summary>
internal static class MemorandumEntityExtensions
{
    /// <summary>
    /// JSON serializer options configured to handle IReadOnlySet and other interfaces.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new CanonicalStateDictionaryConverter(),
            new CoherenceViolationSetConverter()
        }
    };

    /// <summary>
    /// Converts an EF entity to a domain model.
    /// </summary>
    internal static Memorandum ToDomain(this MemorandumEntity entity)
    {
        var canonicalStates = string.IsNullOrEmpty(entity.CanonicalStatesData)
            ? new Dictionary<MemoryLevel, CanonicalState>()
            : JsonSerializer.Deserialize<Dictionary<MemoryLevel, CanonicalState>>(entity.CanonicalStatesData, JsonOptions)
                ?? new Dictionary<MemoryLevel, CanonicalState>();

        var violations = string.IsNullOrEmpty(entity.ViolationsData)
            ? new HashSet<CoherenceViolation>()
            : JsonSerializer.Deserialize<HashSet<CoherenceViolation>>(entity.ViolationsData, JsonOptions)
                ?? new HashSet<CoherenceViolation>();

        return new Memorandum(
            Id: Guid.Parse(entity.Id),
            WorldId: Guid.Parse(entity.WorldId),
            Title: entity.Title,
            Description: entity.Description,
            CanonicalStates: canonicalStates,
            Violations: violations,
            CreatedAt: entity.CreatedAt,
            LastUpdated: entity.LastUpdated
        );
    }
}

/// <summary>
/// Custom JSON converter for Dictionary of CanonicalState that handles IReadOnlySet serialization.
/// </summary>
internal class CanonicalStateDictionaryConverter : JsonConverter<Dictionary<MemoryLevel, CanonicalState>>
{
    public override Dictionary<MemoryLevel, CanonicalState>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var result = new Dictionary<MemoryLevel, CanonicalState>();

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject token");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return result;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName token");

            var keyString = reader.GetString()!;
            var key = Enum.Parse<MemoryLevel>(keyString);

            reader.Read();
            var value = ReadCanonicalState(ref reader);
            result[key] = value;
        }

        return result;
    }

    private static CanonicalState ReadCanonicalState(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for CanonicalState");

        Guid id = Guid.Empty;
        Guid worldId = Guid.Empty;
        var facts = new HashSet<Fact>();
        MemoryLevel memoryLevel = MemoryLevel.Event;
        int version = 1;
        DateTime? lastUpdated = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName");

            var propName = reader.GetString()!;
            reader.Read();

            switch (propName)
            {
                case "Id":
                    id = reader.GetGuid();
                    break;
                case "WorldId":
                    worldId = reader.GetGuid();
                    break;
                case "Facts":
                    facts = ReadFactsSet(ref reader);
                    break;
                case "MemoryLevel":
                    memoryLevel = (MemoryLevel)reader.GetInt32();
                    break;
                case "Version":
                    version = reader.GetInt32();
                    break;
                case "LastUpdated":
                    if (reader.TokenType != JsonTokenType.Null)
                        lastUpdated = reader.GetDateTime();
                    break;
            }
        }

        return new CanonicalState(id, worldId, facts, memoryLevel, version, lastUpdated);
    }

    private static HashSet<Fact> ReadFactsSet(ref Utf8JsonReader reader)
    {
        var facts = new HashSet<Fact>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray for Facts");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return facts;

            var fact = ReadFact(ref reader);
            facts.Add(fact);
        }

        return facts;
    }

    private static Fact ReadFact(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for Fact");

        Guid id = Guid.Empty;
        string content = "";
        FactType factType = FactType.Event;
        MemoryLevel memoryLevel = MemoryLevel.Event;
        var entityReferences = new HashSet<string>();
        string? timeContext = null;
        double confidence = 1.0;
        string? source = null;
        DateTime? createdAt = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName");

            var propName = reader.GetString()!;
            reader.Read();

            switch (propName)
            {
                case "Id":
                    id = reader.GetGuid();
                    break;
                case "Content":
                    content = reader.GetString() ?? "";
                    break;
                case "FactType":
                    factType = (FactType)reader.GetInt32();
                    break;
                case "MemoryLevel":
                    memoryLevel = (MemoryLevel)reader.GetInt32();
                    break;
                case "EntityReferences":
                    entityReferences = ReadStringSet(ref reader);
                    break;
                case "TimeContext":
                    timeContext = reader.TokenType != JsonTokenType.Null ? reader.GetString() : null;
                    break;
                case "Confidence":
                    confidence = reader.GetDouble();
                    break;
                case "Source":
                    source = reader.TokenType != JsonTokenType.Null ? reader.GetString() : null;
                    break;
                case "CreatedAt":
                    if (reader.TokenType != JsonTokenType.Null)
                        createdAt = reader.GetDateTime();
                    break;
            }
        }

        return new Fact(id, content, factType, memoryLevel, entityReferences, timeContext, confidence, source, createdAt);
    }

    private static HashSet<string> ReadStringSet(ref Utf8JsonReader reader)
    {
        var set = new HashSet<string>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray for string set");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return set;

            var value = reader.GetString();
            if (value != null)
                set.Add(value);
        }

        return set;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<MemoryLevel, CanonicalState> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}

/// <summary>
/// Custom JSON converter for HashSet of CoherenceViolation.
/// </summary>
internal class CoherenceViolationSetConverter : JsonConverter<HashSet<CoherenceViolation>>
{
    public override HashSet<CoherenceViolation>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var result = new HashSet<CoherenceViolation>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray for CoherenceViolation set");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return result;

            var violation = ReadViolation(ref reader);
            result.Add(violation);
        }

        return result;
    }

    private static CoherenceViolation ReadViolation(ref Utf8JsonReader reader)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected StartObject for CoherenceViolation");

        Guid id = Guid.Empty;
        string description = "";
        CoherenceViolationType violationType = CoherenceViolationType.StatementContradiction;
        CoherenceSeverity severity = CoherenceSeverity.Warning;
        var involvedFactIds = new HashSet<Guid>();
        DateTime? detectedAt = null;
        DateTime? resolvedAt = null;
        string? resolution = null;
        MemoryLevel? memoryLevel = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected PropertyName");

            var propName = reader.GetString()!;
            reader.Read();

            switch (propName)
            {
                case "Id":
                    id = reader.GetGuid();
                    break;
                case "Description":
                    description = reader.GetString() ?? "";
                    break;
                case "ViolationType":
                    violationType = (CoherenceViolationType)reader.GetInt32();
                    break;
                case "Severity":
                    severity = (CoherenceSeverity)reader.GetInt32();
                    break;
                case "InvolvedFactIds":
                    involvedFactIds = ReadGuidSet(ref reader);
                    break;
                case "DetectedAt":
                    if (reader.TokenType != JsonTokenType.Null)
                        detectedAt = reader.GetDateTime();
                    break;
                case "ResolvedAt":
                    if (reader.TokenType != JsonTokenType.Null)
                        resolvedAt = reader.GetDateTime();
                    break;
                case "Resolution":
                    resolution = reader.TokenType != JsonTokenType.Null ? reader.GetString() : null;
                    break;
                case "MemoryLevel":
                    if (reader.TokenType != JsonTokenType.Null)
                        memoryLevel = (MemoryLevel)reader.GetInt32();
                    break;
            }
        }

        // CoherenceViolation(Guid Id, CoherenceViolationType ViolationType, CoherenceSeverity Severity,
        //                    string Description, IReadOnlySet<Guid> InvolvedFactIds, string? Resolution,
        //                    MemoryLevel? MemoryLevel, DateTime? DetectedAt, DateTime? ResolvedAt)
        return new CoherenceViolation(id, violationType, severity, description, involvedFactIds, resolution, memoryLevel, detectedAt, resolvedAt);
    }

    private static HashSet<Guid> ReadGuidSet(ref Utf8JsonReader reader)
    {
        var set = new HashSet<Guid>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray for Guid set");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                return set;

            set.Add(reader.GetGuid());
        }

        return set;
    }

    public override void Write(Utf8JsonWriter writer, HashSet<CoherenceViolation> value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value);
    }
}
