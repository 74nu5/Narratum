using Microsoft.EntityFrameworkCore;

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
    /// Table des snapshots de pages pour navigation temporelle.
    /// Chaque page générée crée une entrée ici.
    /// </summary>
    public DbSet<PageSnapshotEntity> PageSnapshots { get; set; } = null!;

    /// <summary>
    /// Table des univers : le décor réutilisable (monde, ton, casting, lieux, ouverture) dont
    /// chaque histoire est une partie.
    /// </summary>
    public DbSet<UniverseEntity> Universes { get; set; } = null!;

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

        // Configuration de UniverseEntity
        modelBuilder.Entity<UniverseEntity>()
            .HasKey(u => u.UniverseId);

        modelBuilder.Entity<UniverseEntity>()
            .Property(u => u.UniverseId)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<UniverseEntity>()
            .Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(500);

        modelBuilder.Entity<UniverseEntity>()
            .Property(u => u.CreatedAt)
            .IsRequired();

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

        // Configuration de PageSnapshotEntity
        modelBuilder.Entity<PageSnapshotEntity>()
            .HasKey(p => p.Id);

        modelBuilder.Entity<PageSnapshotEntity>()
            .Property(p => p.SlotName)
            .IsRequired()
            .HasMaxLength(255);

        modelBuilder.Entity<PageSnapshotEntity>()
            .Property(p => p.PageIndex)
            .IsRequired();

        modelBuilder.Entity<PageSnapshotEntity>()
            .Property(p => p.GeneratedAt)
            .IsRequired();

        modelBuilder.Entity<PageSnapshotEntity>()
            .Property(p => p.SerializedState)
            .IsRequired();

        // Index composite sur (SlotName, PageIndex) pour requêtes efficaces
        modelBuilder.Entity<PageSnapshotEntity>()
            .HasIndex(p => new { p.SlotName, p.PageIndex })
            .IsUnique();
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

    /// <summary>
    /// Bible de l'univers sérialisée (monde, style, personnages, lieux) telle que définie à la
    /// création. Sert d'instantané de repli : quand la partie est rattachée à un univers, c'est
    /// ce dernier qui fait foi. Null pour les histoires antérieures à cette fonctionnalité.
    /// </summary>
    public string? SerializedWorld { get; init; }

    /// <summary>
    /// Univers dont cette partie est issue. Null pour les histoires créées avant les univers.
    /// </summary>
    public string? UniverseId { get; init; }
}

/// <summary>
/// Un univers réutilisable : le décor, le ton, le casting, les lieux et la situation de départ.
/// Une même entrée est rejouée par autant de parties (<see cref="SaveSlotMetadata"/>) qu'on veut.
/// </summary>
public record UniverseEntity
{
    /// <summary>Identifiant lisible (ex. <c>univers-20260719-123456</c>).</summary>
    public required string UniverseId { get; init; }

    /// <summary>Nom du monde.</summary>
    public required string Name { get; init; }

    /// <summary>Genre narratif (Fantasy, SciFi…).</summary>
    public required string GenreStyle { get; init; }

    /// <summary>Description du monde : ambiance, époque, règles.</summary>
    public string? Description { get; init; }

    /// <summary>Ton et contraintes d'écriture à tenir.</summary>
    public string? NarrativeStyle { get; init; }

    /// <summary>Casting sérialisé (nom + description).</summary>
    public string? SerializedCharacters { get; init; }

    /// <summary>Lieux sérialisés (nom + description).</summary>
    public string? SerializedLocations { get; init; }

    /// <summary>Situation de départ rejouée par chaque nouvelle partie.</summary>
    public string? OpeningAction { get; init; }

    /// <summary>Modèle de génération retenu à la création.</summary>
    public string? DefaultModel { get; init; }

    /// <summary>Date de création.</summary>
    public required DateTime CreatedAt { get; init; }
}
