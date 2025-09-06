using System.Globalization;
using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Avoid cycles when serializing entity graphs (prevents Swagger generation errors)
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.MaxDepth = 64;
    });

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(240);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
// Identity using string Role (not Guid)
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// JWT Bearer for mobile/API clients (Android)
// Ensure key is at least 256 bits (32 bytes) for HS256
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    // default to a sufficiently long key for development. Replace in production via configuration.
    jwtKey = "VerySecretKey12345VerySecretKey12345"; // 32 chars -> 256 bits
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "TrainingCenter";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "TrainingCenterAudience";
builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

        // Allow JWT to be passed in query string for SignalR
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/ChatHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    // For API calls (paths starting with /api) return 401/403 instead of redirecting to HTML login page.
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            var request = ctx.Request;
            var isApi = request.Path.StartsWithSegments("/api") || request.Headers["Accept"].Any(h => h.Contains("application/json")) || request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isApi)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            var request = ctx.Request;
            var isApi = request.Path.StartsWithSegments("/api") || request.Headers["Accept"].Any(h => h.Contains("application/json")) || request.Headers["X-Requested-With"] == "XMLHttpRequest";
            if (isApi)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .WithExposedHeaders("Content-Disposition");
    });
});


builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.HandshakeTimeout = TimeSpan.FromSeconds(15);
}).AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.PropertyNamingPolicy = null;
});
// Swagger / OpenAPI for APIs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TrainingCenter API", Version = "v1" });

    // JWT Bearer auth for Swagger
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' [space] and then your valid token in the text input below."
    };
    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new string[] { } }
    });

    // Include XML comments if present
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
// Inject UserHelper 
builder.Services.AddScoped<DashboardHelper>();
builder.Services.AddScoped<IUserHelper, UserHelper>();
// Settings service (persisted in DB)
builder.Services.AddScoped<TrainingCenterManagement_MVC.Helpers.ISettingsService, TrainingCenterManagement_MVC.Helpers.SettingsService>();
// Add Role Initializer
builder.Services.AddScoped<RoleInitializer>();
//builder.Services.AddSignalR().AddJsonProtocol(options =>
//{
//    options.PayloadSerializerOptions.PropertyNamingPolicy = null; // íÍÇÝÙ Úáì ÃÓãÇÁ ÇáÎÕÇÆÕ ßãÇ åí (ãËá Id ÈÏáÇð ãä id)
//});
var app = builder.Build();

// Seed roles
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    // تحقق من وجود أي رولات أو بيانات أساسية
    if (!context.Roles.Any() || !context.Courses.Any() || !context.Users.Any())
    {
        var roleInitializer = services.GetRequiredService<RoleInitializer>();
        await roleInitializer.SeedRolesAsync();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var seeder = new SeedDataInitializer(context, userManager);
        await seeder.SeedAllAsync();
    }
     

}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Enable Swagger in all environments (you can restrict to Development if desired)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TrainingCenter API v1");
    c.RoutePrefix = "swagger"; // accessible at /swagger
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRotativa(); // ÈÚÏ UseStaticFiles
app.UseStatusCodePages(context =>
{
    if (context.HttpContext.Response.StatusCode == 403)
    {
        context.HttpContext.Response.Redirect("/Account/AccessDenied");
    }
    if (context.HttpContext.Response.StatusCode == 404)
    {
        context.HttpContext.Response.Redirect("/Home/Error404");
    }
    return Task.CompletedTask;
});

app.UseRouting();
app.UseCors("AllowAll");
app.UseWebSockets();
app.Use(async (context, next) =>
{
    string cookie = string.Empty;
    if (context.Request.Cookies.TryGetValue("Language", out cookie))
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(cookie);
    }
    else
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");
    }
    await next.Invoke();
});
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<ChatHub>("/ChatHub");
app.Run();
