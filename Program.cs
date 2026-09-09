using SalonSuite.Components;
using SalonSuite.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container (Unified SalonSuite Architecture)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient<FirebaseAuthService>();
builder.Services.AddSingleton<SalonDataService>();

var app = builder.Build();

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
