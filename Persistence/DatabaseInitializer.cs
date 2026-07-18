using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Narratum.Persistence;

/// <summary>
/// Initialisation de la base au démarrage via les migrations EF Core.
/// Remplace <c>Database.EnsureCreated()</c>, qui est incompatible avec les migrations.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// Applique les migrations en attente. Si la base a été créée par un ancien
    /// <c>EnsureCreated()</c> (schéma présent, mais pas de table d'historique de migrations),
    /// la migration initiale est « baselinée » : marquée comme appliquée sans réexécuter son DDL,
    /// afin que <c>Database.Migrate()</c> ne tente pas de recréer des tables déjà existantes.
    /// </summary>
    public static void InitializeNarratumDatabase(this NarrativumDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        var appliedMigrations = db.Database.GetAppliedMigrations().ToList();

        // Aucune migration enregistrée mais le schéma existe déjà => base héritée d'EnsureCreated().
        if (appliedMigrations.Count == 0 && LegacySchemaExists(db))
        {
            BaselineInitialMigration(db);
        }

        db.Database.Migrate();
    }

    /// <summary>
    /// Vrai si une table cœur du schéma existe déjà (base créée hors migrations).
    /// Ouvre puis restaure l'état de la connexion : la laisser épinglée ouverte perturberait
    /// le <c>Migrate()</c> suivant (migrations partiellement appliquées).
    /// </summary>
    private static bool LegacySchemaExists(NarrativumDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
            connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'PageSnapshots';";

            return Convert.ToInt64(command.ExecuteScalar() ?? 0L) > 0;
        }
        finally
        {
            if (wasClosed)
                connection.Close();
        }
    }

    /// <summary>
    /// Crée la table d'historique des migrations et y insère la migration initiale
    /// comme déjà appliquée, sans exécuter son DDL.
    /// </summary>
    private static void BaselineInitialMigration(NarrativumDbContext db)
    {
        var history = db.GetService<IHistoryRepository>();
        var initialMigration = db.Database.GetMigrations().First();

        db.Database.ExecuteSqlRaw(history.GetCreateIfNotExistsScript());
        db.Database.ExecuteSqlRaw(
            history.GetInsertScript(new HistoryRow(initialMigration, ProductInfo.GetVersion())));
    }
}
