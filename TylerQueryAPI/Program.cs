using Microsoft.AspNetCore.HttpOverrides;
using TylerInfoAPI.Options;
using TylerInfoAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Needed by TokenService (your earlier DI error)
builder.Services.AddMemoryCache();

// Options binding
builder.Services.Configure<TcmOptions>(builder.Configuration.GetSection("Tcm"));
builder.Services.Configure<TylerOptions>(builder.Configuration.GetSection("Tyler"));

// Http clients
builder.Services.AddHttpClient("tcm");
builder.Services.AddHttpClient("tyler");

// DI
builder.Services.AddScoped<ITcmService, TcmSoapService>();
builder.Services.AddScoped<ITylerService, TylerService>();
builder.Services.AddScoped<ITokenService, TokenService>();

var app = builder.Build();

// Helps when behind IIS / reverse proxy (makes scheme/https correct)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Serve parcel.html when browsing the app root
var df = new DefaultFilesOptions();
df.DefaultFileNames.Clear();
df.DefaultFileNames.Add("parcel.html");
df.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(df);

// Serve wwwroot files
app.UseStaticFiles();

// Optional (fine to keep if you have proper https binding)
app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

app.Run();