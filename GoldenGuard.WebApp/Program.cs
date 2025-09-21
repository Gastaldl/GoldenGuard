var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

// HttpClient do ApiClient com BaseAddress lida do appsettings
builder.Services.AddHttpClient<GoldenGuard.WebApp.Services.ApiClient>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["Api:BaseUrl"];

    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new InvalidOperationException("Config faltando: Api:BaseUrl no appsettings.json");

    // tolera se alguém colocar sem esquema
    if (!baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        baseUrl = "https://" + baseUrl.TrimStart('/');

    http.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();