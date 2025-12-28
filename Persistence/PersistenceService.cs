using Microsoft.EntityFrameworkCore;
using Narratum.Core;
using Narratum.State;

namespace Narratum.Persistence;

/// <summary>
/// Implémentation de IPersistenceService utilisant EF Core et SQLite.
/// Gère la sauvegarde et le chargement des états narratifs complets.
/// </summary>
public class PersistenceService : IPersistenceService
{
    private readonly NarrativumDbContext _dbContext;
    private readonly ISnapshotService _snapshotService;

    /// <summary>
    /// Initialise une nouvelle instance du service de persistance.
    /// </summary>
    /// <param name="dbContext">Contexte Entity Framework Core</param>
    /// <param name="snapshotService">Service de création de snapshots</param>
    public PersistenceService(
        NarrativumDbContext dbContext,
        ISnapshotService snapshotService)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(snapshotService);

        _dbContext = dbContext;
        _snapshotService = snapshotService;
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> SaveStateAsync(string slotName, StoryState state)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return Result<Unit>.Fail("Slot name cannot be empty");

        ArgumentNullException.ThrowIfNull(state);

        try
        {
            // Créer le snapshot
            var snapshot = _snapshotService.CreateSnapshot(state);

            // Vérifier si le slot existe déjà
            var existingSnapshot = await _dbContext.SavedStates
                .FirstOrDefaultAsync(s => s.SlotName == slotName);

            if (existingSnapshot != null)
            {
                // Supprimer l'ancien snapshot
                _dbContext.SavedStates.Remove(existingSnapshot);
            }

            // Créer la nouvelle entrée
            var newSnapshot = new SaveStateSnapshot
            {
                Id = Guid.NewGuid(),
                SlotName = slotName,
                SnapshotData = System.Text.Json.JsonSerializer.Serialize(snapshot),
                SavedAt = DateTime.UtcNow,
                SnapshotVersion = 1,
                IntegrityHash = snapshot.IntegrityHash
            };

            _dbContext.SavedStates.Add(newSnapshot);

            // Mettre à jour ou créer les métadonnées
            var metadata = await _dbContext.SaveSlots
                .FirstOrDefaultAsync(m => m.SlotName == slotName);

            if (metadata != null)
            {
                var updatedMetadata = metadata with
                {
                    LastSavedAt = DateTime.UtcNow,
                    TotalEvents = snapshot.TotalEventCount,
                    CurrentChapterId = snapshot.CurrentChapterId
                };

                _dbContext.SaveSlots.Update(updatedMetadata);
            }
            else
            {
                var newMetadata = new SaveSlotMetadata
                {
                    SlotName = slotName,
                    LastSavedAt = DateTime.UtcNow,
                    TotalEvents = snapshot.TotalEventCount,
                    CurrentChapterId = snapshot.CurrentChapterId,
                    DisplayName = slotName,
                    Description = $"Saved at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
                };

                _dbContext.SaveSlots.Add(newMetadata);
            }

            // Sauvegarder dans la base de données
            await _dbContext.SaveChangesAsync();

            return Result<Unit>.Ok(Unit.Default());
        }
        catch (Exception ex)
        {
            return Result<Unit>.Fail($"Failed to save state: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<StoryState>> LoadStateAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return Result<StoryState>.Fail("Slot name cannot be empty");

        try
        {
            // Récupérer le snapshot sauvegardé
            var savedSnapshot = await _dbContext.SavedStates
                .FirstOrDefaultAsync(s => s.SlotName == slotName);

            if (savedSnapshot == null)
                return Result<StoryState>.Fail($"No saved state found for slot '{slotName}'");

            // Désérialiser le snapshot
            var snapshot = System.Text.Json.JsonSerializer.Deserialize<StateSnapshot>(
                savedSnapshot.SnapshotData,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });

            if (snapshot == null)
                return Result<StoryState>.Fail("Failed to deserialize snapshot");

            // Restaurer l'état à partir du snapshot
            return _snapshotService.RestoreFromSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            return Result<StoryState>.Fail($"Failed to load state: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteStateAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return Result<Unit>.Fail("Slot name cannot be empty");

        try
        {
            // Récupérer et supprimer le snapshot
            var snapshot = await _dbContext.SavedStates
                .FirstOrDefaultAsync(s => s.SlotName == slotName);

            if (snapshot != null)
            {
                _dbContext.SavedStates.Remove(snapshot);
            }

            // Récupérer et supprimer les métadonnées
            var metadata = await _dbContext.SaveSlots
                .FirstOrDefaultAsync(m => m.SlotName == slotName);

            if (metadata != null)
            {
                _dbContext.SaveSlots.Remove(metadata);
            }

            if (snapshot != null || metadata != null)
            {
                await _dbContext.SaveChangesAsync();
                return Result<Unit>.Ok(Unit.Default());
            }

            return Result<Unit>.Fail($"No saved state found for slot '{slotName}'");
        }
        catch (Exception ex)
        {
            return Result<Unit>.Fail($"Failed to delete state: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> ListSavedStatesAsync()
    {
        try
        {
            var slotNames = await _dbContext.SaveSlots
                .OrderByDescending(s => s.LastSavedAt)
                .Select(s => s.SlotName)
                .ToListAsync();

            return Result<IReadOnlyList<string>>.Ok(slotNames.AsReadOnly());
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<string>>.Fail($"Failed to list saved states: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> StateExistsAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return Result<bool>.Fail("Slot name cannot be empty");

        try
        {
            var exists = await _dbContext.SaveSlots
                .AnyAsync(s => s.SlotName == slotName);

            return Result<bool>.Ok(exists);
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail($"Failed to check state existence: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Result<SaveStateMetadata>> GetStateMetadataAsync(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName))
            return Result<SaveStateMetadata>.Fail("Slot name cannot be empty");

        try
        {
            var metadata = await _dbContext.SaveSlots
                .FirstOrDefaultAsync(m => m.SlotName == slotName);

            if (metadata == null)
                return Result<SaveStateMetadata>.Fail($"No metadata found for slot '{slotName}'");

            var result = new SaveStateMetadata(
                SlotName: metadata.SlotName,
                SavedAt: metadata.LastSavedAt,
                TotalEvents: metadata.TotalEvents,
                CurrentChapterId: metadata.CurrentChapterId
            );

            return Result<SaveStateMetadata>.Ok(result);
        }
        catch (Exception ex)
        {
            return Result<SaveStateMetadata>.Fail($"Failed to get metadata: {ex.Message}", ex);
        }
    }
}
