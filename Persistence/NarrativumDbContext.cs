using Microsoft.EntityFrameworkCore;
using Narratum.State;

namespace Narratum.Persistence;

/// <summary>
/// Entity Framework Core DbContext pour Narratum.
/// Gère la persistance des états narratifs et snapshots avec SQLite.
/// </summary>
public class NarrativumDbContext : DbContext
{
    /// <summary>
    /// Initialise une nouvelle instance du contexte avec les options spécifiées.
    /// </summary>
    /// <param name="options">Options de configuration du DbContext</param>
    public NarrativumDbContext(DbContextOptions<NarrativumDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Table des snapshots d'état sauvegardés.
    /// </summary>
    public DbSet<SaveStateSnapshot> SavedStates { get; set; } = null!;

    /// <summary>
    /// Table des métadonnées de sauvegarde.
    /// </summary>
    public DbSet<SaveSlotMetadata> SaveSlots { get; set; } = null!;

    /// <summary>
    /// Configure le modèle EF Core et les relations.
    /// </summary>
    /// <param name="modelBuilder">Builder pour configurer le modèle</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuration de SaveStateSnapshot
        modelBuilder.Entity<SaveStateSnapshot>()
            .HasKey(s => s.Id);

        modelBuilder.Entity<SaveStateSnapshot>()
            .Property(s => s.SlotName)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<SaveStateSnapshot>()
            .Property(s => s.SnapshotData)
            .IsRequired();

        modelBuilder.Entity<SaveStateSnapshot>()
            .Property(s => s.SavedAt)
            .IsRequired();

        modelBuilder.Entity<SaveStateSnapshot>()
            .HasIndex(s => s.SlotName)
            .IsUnique();

        // Configuration de SaveSlotMetadata
        modelBuilder.Entity<SaveSlotMetadata>()
            .HasKey(m => m.SlotName);

        modelBuilder.Entity<SaveSlotMetadata>()
            .Property(m => m.SlotName)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<SaveSlotMetadata>()
            .Property(m => m.LastSavedAt)
            .IsRequired();

        modelBuilder.Entity<SaveSlotMetadata>()
            .Property(m => m.TotalEvents)
            .IsRequired();

        modelBuilder.Entity<SaveSlotMetadata>()
            .Property(m => m.CurrentChapterId)
            .IsRequired();
    }

    /// <summary>
    /// Configure la chaîne de connexion SQLite.
    /// </summary>
    /// <param name="optionsBuilder">Builder pour les options du DbContext</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=narratum.db");
        }
    }
}

/// <summary>
/// Entité représentant un état sauvegardé dans la base de données.
/// </summary>
public record SaveStateSnapshot
{
    /// <summary>
    /// Identifiant unique de la sauvegarde.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Nom du slot de sauvegarde.
    /// </summary>
    public required string SlotName { get; init; }

    /// <summary>
    /// Données sérialisées du StateSnapshot (JSON ou binaire).
    /// </summary>
    public required string SnapshotData { get; init; }

    /// <summary>
    /// Timestamp de sauvegarde.
    /// </summary>
    public required DateTime SavedAt { get; init; }

    /// <summary>
    /// Version du format pour migrations futures.
    /// </summary>
    public required int SnapshotVersion { get; init; }

    /// <summary>
    /// Hash SHA256 pour vérifier l'intégrité (optionnel).
    /// </summary>
    public string? IntegrityHash { get; init; }
}

/// <summary>
/// Métadonnées d'un slot de sauvegarde.
/// </summary>
public record SaveSlotMetadata
{
    /// <summary>
    /// Nom unique du slot.
    /// </summary>
    public required string SlotName { get; init; }

    /// <summary>
    /// Timestamp de la dernière sauvegarde.
    /// </summary>
    public required DateTime LastSavedAt { get; init; }

    /// <summary>
    /// Nombre d'événements au moment de la sauvegarde.
    /// </summary>
    public required int TotalEvents { get; init; }

    /// <summary>
    /// ID du chapitre actif.
    /// </summary>
    public Guid? CurrentChapterId { get; init; }

    /// <summary>
    /// Nom affiché du slot (optionnel).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// Description de la sauvegarde (optionnel).
    /// </summary>
    public string? Description { get; init; }
}
