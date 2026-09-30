var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5280");
}

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Data source: Mock (default) or Smartcar (live Tesla via Smartcar)
// Prefer Smartcar:UseMockData; fall back to legacy Tesla:UseMockData for compatibility
var useMock = builder.Configuration.GetValue("Smartcar:UseMockData",
    builder.Configuration.GetValue("Tesla:UseMockData", true));

if (useMock)
{
    builder.Services.AddSingleton<MyTeslaGuard.Services.ITeslaDataService, MyTeslaGuard.Services.MockTeslaDataService>();
}
else
{
    builder.Services.AddSingleton<MyTeslaGuard.Services.ITeslaDataService, MyTeslaGuard.Services.SmartcarDataService>();
}

builder.Services.AddHttpClient("nominatim", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyTeslaGuard/1.0 (sunntoguard.duckdns.org; contact via repo)");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(12);
});

builder.Services.AddHttpClient("smartcar", client =>
{
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(25);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
