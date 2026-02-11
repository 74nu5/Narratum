# Correction - Enregistrement ILlmClient dans le DI

## Problème 1 : Service non enregistré

Lors du test de la Phase 4 avec le Playground, erreur :
```
✗ Erreur de résolution du client: No service for type 'Narratum.Orchestration.Llm.ILlmClient' has been registered.
```

### Cause
`AddNarratumFoundryLocal()` enregistrait seulement `ILlmClientFactory` mais pas `ILlmClient` directement dans le conteneur DI.

### Solution
Ajout de l'enregistrement de `ILlmClient` dans `LlmServiceCollectionExtensions.AddNarratumLlm()` :

```csharp
// Register ILlmClient as a factory-created singleton
services.TryAddSingleton<ILlmClient>(sp =>
{
    var factory = sp.GetRequiredService<ILlmClientFactory>();
    return factory.CreateClientAsync().GetAwaiter().GetResult();
});
```

## Problème 2 : Configuration Web manquante

Deuxième erreur :
```
fail: Narratum.Llm.Lifecycle.FoundryLocalLifecycleManager[0]
      Web service configuration was not provided.
```

### Cause
La propriété `Web` n'était pas initialisée dans la configuration Foundry Local, ce qui causait une `NullReferenceException` quand le code essayait d'accéder à `foundryConfig.Web.Urls`.

### Solution
Initialisation explicite de `Web` dans `FoundryLocalLifecycleManager.InitializeAsync()` :

```csharp
var foundryConfig = new Microsoft.AI.Foundry.Local.Configuration
{
    AppName = "Narratum",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
    ModelCacheDir = string.IsNullOrEmpty(_config.CacheDirectory) ? null : _config.CacheDirectory,
    Web = new Microsoft.AI.Foundry.Local.Configuration.WebService
    {
        Urls = "http://127.0.0.1:5272"
    }
};
```

**Note :** Selon la documentation Microsoft Foundry Local, l'objet `Web` DOIT être initialisé avant `StartWebServiceAsync()`.

## Fichiers modifiés

- `Llm/DependencyInjection/LlmServiceCollectionExtensions.cs` (lignes 24-30)
- `Llm/Lifecycle/FoundryLocalLifecycleManager.cs` (lignes 39-51)

## Validation

✅ Build réussi : 0 erreurs, 0 warnings  
✅ 52 tests Llm.Tests passent  
✅ `ILlmClient` peut maintenant être injecté directement  
✅ Configuration Web correctement initialisée  

## Impact

Tous les projets utilisant `AddNarratumFoundryLocal()` ou `AddNarratumOllama()` peuvent maintenant résoudre `ILlmClient` directement depuis le DI et le service Foundry Local démarre correctement avec sa configuration Web.

```csharp
services.AddNarratumFoundryLocal(defaultModel: "phi-4-mini");
var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<ILlmClient>(); // ✅ Fonctionne
```
