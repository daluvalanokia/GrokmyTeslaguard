var builder = WebApplication.CreateBuilder(args);

// Only force 0.0.0.0:5280 when explicitly in Development / Codespaces.
// Production (e.g. nginx reverse proxy on duckdns) should set ASPNETCORE_URLS itself.
if (builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5280");
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<MyTeslaGuard.Services.ITeslaDataService, MyTeslaGuard.Services.MockTeslaDataService>();
builder.Services.AddSignalR();

// Nominatim requires a descriptive User-Agent; browser fetch cannot set this reliably
// and is often blocked on public domains — we proxy geocode server-side instead.
builder.Services.AddHttpClient("nominatim", client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("MyTeslaGuard/1.0 (sunntoguard.duckdns.org; contact via repo)");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    client.Timeout = TimeSpan.FromSeconds(12);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

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
