
using RaceDay.Mvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Read the API base URL from appsettings.json.
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];

if (string.IsNullOrWhiteSpace(apiBaseUrl) ||
    !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var apiUri))
{
    throw new InvalidOperationException(
        "A valid ApiSettings:BaseUrl must be configured in appsettings.json.");
}

// Register MVC.
builder.Services.AddControllersWithViews();

// Register session services.
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Register HTTP context access.
builder.Services.AddHttpContextAccessor();

// Register the RaceDay API HTTP client.
builder.Services.AddHttpClient("RaceDayApi", client =>
{
    client.BaseAddress = apiUri;
});

// Register ApiService.
builder.Services.AddHttpClient<ApiService>(client =>
{
    client.BaseAddress = apiUri;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session must be enabled before authorization.
app.UseSession();
app.UseAuthorization();

// Configure the default MVC route.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
