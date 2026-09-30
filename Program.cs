var builder = WebApplication.CreateBuilder(args);

// Only force 0.0.0.0:5280 in Development / Codespaces.
// Production (nginx / duckdns) should set ASPNETCORE_URLS itself.
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5280");
}

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Tesla data source: mock (default) or live Fleet API
var useMock = builder.Configuration.GetValue("Tesla:UseMockData", true);
if (useMock)
{
    builder.Services.AddSingleton<MyTeslaGuard.Services.ITeslaDataService, MyTeslaGuard.Services.MockTeslaDataService>();
}
else
{
    builder.Services.AddSingleton<MyTeslaGuard.Services.ITeslaDataService, MyTeslaGuard.Services.FleetTeslaDataService>();
}

builder.Services.AddHttpClient("nominatim", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyTeslaGuard/1.0 (sunntoguard.duckdns.org; contact via repo)");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(12);
});

builder.Services.AddHttpClient("tesla", client =>
{
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(20);
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
