using Narratum.Web.Components;
using Narratum.Persistence;
using Narratum.Llm.DependencyInjection;
using Narratum.Llm.Configuration;
using Narratum.Web.Services;
using Narratum.Orchestration.Services;
using Narratum.Core;

using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Fluent UI Components
builder.Services.AddFluentUIComponents();

// Add Blazor Server with Interactive Server Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Narratum Persistence (SQLite EF Core).
// A factory lets StoryRepository create a fresh, short-lived DbContext per operation,
// avoiding the pitfalls of a long-lived circuit-scoped context in Blazor Server.
// A scoped shim keeps other consumers (PersistenceService, startup EnsureCreated) working.
builder.Services.AddDbContextFactory<NarrativumDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Narratum") ?? "Data Source=narratum.db"));
builder.Services.AddScoped<NarrativumDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<NarrativumDbContext>>().CreateDbContext());

builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<IStoryRepository, StoryRepository>();
builder.Services.AddScoped<PersistenceService>();

// Add Narratum LLM (Foundry Local)
var llmConfig = new LlmClientConfig
{
    Provider = LlmProviderType.FoundryLocal,
    DefaultModel = "phi-4-mini"
};
builder.Services.AddNarratumLlm(llmConfig);

// Add Narratum Orchestration
builder.Services.AddScoped<FullOrchestrationService>();

// Add Web Services
builder.Services.AddScoped<StoryLibraryService>();
builder.Services.AddScoped<ModelSelectionService>();
builder.Services.AddSingleton<ModelCatalogService>();
builder.Services.AddSingleton<AzureFoundryState>();
builder.Services.AddSingleton<ImageStorageService>();
builder.Services.AddScoped<IModelResolver>(sp => sp.GetRequiredService<ModelSelectionService>());
builder.Services.AddScoped<IGenerationService, GenerationService>();
builder.Services.AddScoped<ExpertModeService>();

var app = builder.Build();

// Apply EF Core migrations (baselines a legacy EnsureCreated database if needed).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NarrativumDbContext>();
    db.InitializeNarratumDatabase();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve runtime-generated page images (written outside wwwroot) under /generated-images.
var imageStorage = app.Services.GetRequiredService<ImageStorageService>();
app.UseStaticFiles(new Microsoft.AspNetCore.Builder.StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(imageStorage.RootPath),
    RequestPath = ImageStorageService.RequestPath
});

app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

// Exposed so WebApplicationFactory-based integration tests can boot the real
// DI container and verify the service graph (top-level statements otherwise
// generate an internal Program class).
public partial class Program { }
