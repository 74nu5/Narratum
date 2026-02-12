using Narratum.Web.Components;
using Narratum.Persistence;
using Narratum.Llm.DependencyInjection;
using Narratum.Llm.Configuration;
using Narratum.Web.Services;
using Narratum.Orchestration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add Fluent UI Components
builder.Services.AddFluentUIComponents();

// Add Blazor Server with Interactive Server Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add Narratum Persistence (SQLite EF Core)
builder.Services.AddDbContext<NarrativumDbContext>(options =>
    options.UseSqlite("Data Source=narratum.db"));

builder.Services.AddScoped<ISnapshotService, SnapshotService>();
builder.Services.AddScoped<PersistenceService>();

// Add Narratum LLM (Foundry Local)
var llmConfig = new LlmClientConfig
{
    Provider = LlmProviderType.FoundryLocal,
    DefaultModel = "Phi-4-mini"
};
builder.Services.AddNarratumLlm(llmConfig);

// Add Narratum Orchestration
builder.Services.AddScoped<FullOrchestrationService>();

// Add Web Services
builder.Services.AddScoped<StoryLibraryService>();
builder.Services.AddScoped<ModelSelectionService>();
builder.Services.AddScoped<GenerationService>();
builder.Services.AddScoped<ExpertModeService>();

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NarrativumDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
