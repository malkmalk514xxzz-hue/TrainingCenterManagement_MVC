using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Rotativa.AspNetCore;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;

var builder = WebApplication.CreateBuilder(args);

// ====================== Basic Services ======================
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.MaxDepth = 64;
    });
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1 جيجا
});

// DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(240);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Identity
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

// ====================== JWT Configuration ======================
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"];

if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    jwtKey = "SuperSecretKeyForDevelopmentOnly1234567890ABCDEF1234567890";
    Console.WriteLine("⚠️ Using fallback JWT development key - Change this in production!");
}
else
{
    Console.WriteLine($"✅ JWT Key loaded successfully. Length: {jwtKey.Length} characters");
}

var jwtIssuer = jwtSection["Issuer"] ?? "TrainingCenter";
var jwtAudience = jwtSection["Audience"] ?? "TrainingCenterAudience";

// ====================== Authentication Configuration ======================
// في ملف Program.cs - ابحث عن هذا الجزء وقم بتعديله
builder.Services.AddAuthentication(options =>
{
    // التعديل: اجعل المخطط الذكي هو الافتراضي
    options.DefaultAuthenticateScheme = "SmartScheme";
    options.DefaultChallengeScheme = "SmartScheme";
})

// مخطط ذكي للتمييز بين JWT و Cookie
// في ملف Program.cs
.AddPolicyScheme("SmartScheme", "JWT or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var accessTokenQuery = context.Request.Query["access_token"].FirstOrDefault();

        // Logging للتشخيص (ستراه في Output أو Console)
        Console.WriteLine($"[SmartScheme] Path: {context.Request.Path}");
        Console.WriteLine($"[SmartScheme] Authorization Header: '{authHeader}'");
        Console.WriteLine($"[SmartScheme] Query access_token: '{accessTokenQuery}'");

        if (!string.IsNullOrEmpty(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[SmartScheme] → Forwarding to JwtBearer");
            return JwtBearerDefaults.AuthenticationScheme;
        }

        if (!string.IsNullOrEmpty(accessTokenQuery))
        {
            Console.WriteLine("[SmartScheme] → Forwarding to JwtBearer (from Query)");
            return JwtBearerDefaults.AuthenticationScheme;
        }

        Console.WriteLine("[SmartScheme] → Forwarding to Cookie (default)");
        return IdentityConstants.ApplicationScheme;
    };
})
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        NameClaimType = ClaimTypes.NameIdentifier,
        RoleClaimType = ClaimTypes.Role
    };

    //options.Events = new JwtBearerEvents
    //{
    //    OnMessageReceived = context =>
    //    {
    //        var accessToken = context.Request.Query["access_token"];
    //        if (!string.IsNullOrEmpty(accessToken) &&
    //            context.Request.Path.StartsWithSegments("/ChatHub"))
    //        {
    //            context.Token = accessToken;
    //            Console.WriteLine("[SignalR] Token received from query string");
    //        }
    //        return Task.CompletedTask;
    //    }
    //};
});

// Cookie Configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = ctx =>
        {
            if (IsApiRequest(ctx.Request))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = ctx =>
        {
            if (IsApiRequest(ctx.Request))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        }
    };
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader()
               .WithExposedHeaders("Content-Disposition");
    });

    options.AddPolicy("MobileClientPolicy", policy =>
    {
        policy.WithOrigins(
                "https://10.0.2.2:7284",
                "http://10.0.2.2:5272",
                "https://localhost:7284",
                "http://localhost:5272",
                "https://192.168.1.100:7284"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Content-Disposition");
    });
});

// SignalR
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
}).AddJsonProtocol(options => options.PayloadSerializerOptions.PropertyNamingPolicy = null);


// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TrainingCenter Management API",
        Version = "v1"
    });

    // أضف DocumentFilter الجديد
    c.DocumentFilter<JwtBearerDocumentFilter>();
});

// Custom Services
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<DashboardHelper>();
builder.Services.AddScoped<IUserHelper, UserHelper>();
builder.Services.AddScoped<RoleInitializer>();

// ====================== Build App ======================

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.WebRootPath, "uploads")),
    RequestPath = "/uploads",
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.Append("Cache-Control", "private, max-age=3600")
});

// Seed Data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    if (!context.Roles.Any() || !context.Users.Any())
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

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TrainingCenter API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRotativa();

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.StatusCode == 403)
        response.Redirect("/Account/AccessDenied");
    else if (response.StatusCode == 404)
        response.Redirect("/Home/Error404");
});
app.Use(async (context, next) =>
{
    if (context.Request.Cookies.TryGetValue("Language", out var langCookie))
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo(langCookie);
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(langCookie);
    }
    else
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("en");
    }
    await next();
});

// Middleware خاص بـ SignalR (نقل التوكن)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/ChatHub"))
    {
        var accessToken = context.Request.Query["access_token"];
        if (!string.IsNullOrEmpty(accessToken))
        {
            context.Request.Headers["Authorization"] = $"Bearer {accessToken}";
            Console.WriteLine("[SignalR Middleware] Token moved from query to header");
        }
    }
    await next();
});

app.UseRouting();

// CORS يجب أن يكون قبل Authentication
app.UseCors("AllowAll");

app.UseWebSockets();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<ChatHub>("/ChatHub");

app.Run();

// ====================== Helper ======================
static bool IsApiRequest(HttpRequest request)
{
    return request.Path.StartsWithSegments("/api") ||
           request.Headers["Accept"].Any(h => h?.Contains("application/json") == true) ||
           request.Headers["X-Requested-With"] == "XMLHttpRequest";
}