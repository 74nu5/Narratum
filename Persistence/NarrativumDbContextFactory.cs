using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Narratum.Persistence;

/// <summary>
/// Fabrique utilisée UNIQUEMENT au moment du design (outils <c>dotnet ef</c>).
/// Permet de générer et d'appliquer des migrations depuis le projet Persistence
/// sans démarrer l'application Web (qui cible un TFM Windows et initialise Foundry).
/// L'application réelle configure le DbContext via l'injection de dépendances
/// (voir <c>Web/Program.cs</c>) ; cette fabrique n'est jamais utilisée à l'exécution.
/// </summary>
public sealed class NarrativumDbContextFactory : IDesignTimeDbContextFactory<NarrativumDbContext>
{
    public NarrativumDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NarrativumDbContext>()
            .UseSqlite("Data Source=narratum.db")
            .Options;

        return new NarrativumDbContext(options);
    }
}
