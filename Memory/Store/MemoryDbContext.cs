namespace Narratum.Memory.Store;

using Microsoft.EntityFrameworkCore;
using Narratum.Memory.Store.Entities;

/// <summary>
/// Entity Framework Core DbContext for Memory persistence.
/// Provides access to Memorandum and CoherenceViolation entities stored in SQLite.
/// </summary>
public class MemoryDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the MemoryDbContext.
    /// </summary>
    /// <param name="options">The DbContext options configuration.</param>
    public MemoryDbContext(DbContextOptions<MemoryDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the Memorandum entities.
    /// </summary>
    public DbSet<MemorandumEntity> Memoria { get; set; } = null!;

    /// <summary>
    /// Gets or sets the CoherenceViolation entities.
    /// </summary>
    public DbSet<CoherenceViolationEntity> CoherenceViolations { get; set; } = null!;

    /// <summary>
    /// Configures the model for the database.
    /// Defines table names, column mappings, relationships, and constraints.
    /// </summary>
    /// <param name="modelBuilder">The model builder for EF Core configuration.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure MemorandumEntity
        modelBuilder.Entity<MemorandumEntity>(entity =>
        {
            // Table and key configuration
            entity.ToTable("Memoria");
            entity.HasKey(e => e.Id);

            // Properties configuration
            entity.Property(e => e.Id)
                .IsRequired()
                .HasMaxLength(36); // GUID string length

            entity.Property(e => e.WorldId)
                .IsRequired()
                .HasMaxLength(36);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property(e => e.Description)
                .HasColumnType("TEXT");

            entity.Property(e => e.CanonicalStatesData)
                .HasColumnType("TEXT");

            entity.Property(e => e.ViolationsData)
                .HasColumnType("TEXT");

            entity.Property(e => e.SerializedData)
                .HasColumnType("TEXT");

            entity.Property(e => e.ContentHash)
                .HasMaxLength(64); // SHA256 hex string

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.LastUpdated)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.StoredAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.AuditUpdatedAt);

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.DeletedAt);

            // Indexes for query performance
            entity.HasIndex(e => e.WorldId)
                .HasDatabaseName("IX_Memoria_WorldId");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Memoria_CreatedAt");

            entity.HasIndex(e => new { e.WorldId, e.CreatedAt })
                .HasDatabaseName("IX_Memoria_WorldId_CreatedAt");

            entity.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_Memoria_IsDeleted");

            // Relationships
            entity.HasMany(e => e.Violations)
                .WithOne(v => v.Memorandum)
                .HasForeignKey(v => v.MemorandumId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CoherenceViolationEntity
        modelBuilder.Entity<CoherenceViolationEntity>(entity =>
        {
            // Table and key configuration
            entity.ToTable("CoherenceViolations");
            entity.HasKey(e => e.Id);

            // Properties configuration
            entity.Property(e => e.Id)
                .IsRequired()
                .HasMaxLength(36);

            entity.Property(e => e.MemorandumId)
                .IsRequired()
                .HasMaxLength(36);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasColumnType("TEXT");

            entity.Property(e => e.Type)
                .IsRequired()
                .HasDefaultValue(0);

            entity.Property(e => e.ConflictingFact1)
                .HasColumnType("TEXT");

            entity.Property(e => e.ConflictingFact2)
                .HasColumnType("TEXT");

            entity.Property(e => e.DetectedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.Severity)
                .IsRequired()
                .HasDefaultValue(1); // Warning as default

            entity.Property(e => e.SerializedData)
                .HasColumnType("TEXT");

            entity.Property(e => e.StoredAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.Property(e => e.DeletedAt);

            // Indexes for query performance
            entity.HasIndex(e => e.MemorandumId)
                .HasDatabaseName("IX_CoherenceViolations_MemorandumId");

            entity.HasIndex(e => e.Type)
                .HasDatabaseName("IX_CoherenceViolations_Type");

            entity.HasIndex(e => e.Severity)
                .HasDatabaseName("IX_CoherenceViolations_Severity");

            entity.HasIndex(e => e.DetectedAt)
                .HasDatabaseName("IX_CoherenceViolations_DetectedAt");

            entity.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_CoherenceViolations_IsDeleted");

            // Relationships
            entity.HasOne(e => e.Memorandum)
                .WithMany(m => m.Violations)
                .HasForeignKey(e => e.MemorandumId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
