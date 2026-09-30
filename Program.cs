var builder = WebApplication.CreateBuilder(args);

// Bind to all interfaces so Codespaces / Docker port forwarding works
builder.WebHost.UseUrls("http://0.0.0.0:5280");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<MyTeslaGuard.Services.ITeslaDataService, MyTeslaGuard.Services.MockTeslaDataService>();
builder.Services.AddSignalR();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // Only use HSTS outside development
    app.UseHsts();
}

// Skip HTTPS redirection in Development so the Codespaces HTTP port works cleanly
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
