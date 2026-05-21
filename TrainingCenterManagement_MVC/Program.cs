using Messaging_Chat_Application_MahmoudHakim.Hubs;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Rotativa.AspNetCore;
using System.Globalization;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Helpers;
using TrainingCenterManagement_MVC.Models;

var builder = WebApplication.CreateBuilder(args);

// ====================== Basic Services ======================
builder.Services.AddHttpClient();
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.MaxDepth = 64;
    });

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1024 * 1024 * 1024; // 1 GB
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

// Cookie Configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// ====================== CORS ======================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Disposition");
    });
});

// SignalR
builder.Services.AddSignalR(options =>
{
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
}).AddJsonProtocol(options => options.PayloadSerializerOptions.PropertyNamingPolicy = null);

// In-memory cache (for AI config caching)
builder.Services.AddMemoryCache();

// Custom Services
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<DashboardHelper>();
builder.Services.AddScoped<TraineeDashboardHelper>();
builder.Services.AddScoped<TrainerDashboardHelper>();
builder.Services.AddScoped<AdminDashboardHelper>();
builder.Services.AddScoped<ReceptionistDashboardHelper>();
builder.Services.AddScoped<IUserHelper, UserHelper>();
builder.Services.AddScoped<RoleInitializer>();
builder.Services.AddScoped<TrainingCenterManagement_MVC.Services.IExamService,
                           TrainingCenterManagement_MVC.Services.ExamService>();
builder.Services.AddScoped<TrainingCenterManagement_MVC.Services.ILectureResourceService,
                           TrainingCenterManagement_MVC.Services.LectureResourceService>();
builder.Services.AddScoped<TrainingCenterManagement_MVC.Services.IAIPermissionService,
                           TrainingCenterManagement_MVC.Services.AIPermissionService>();
builder.Services.AddScoped<TrainingCenterManagement_MVC.Services.IAIAssistantService,
                           TrainingCenterManagement_MVC.Services.AIAssistantService>();


// تسجيل خدمة المراقبة المستمرة كل 30 ثانية لحساب الشام كاش
builder.Services.AddHostedService<TrainingCenterManagement_MVC.Services.ShamCashMonitorService>();

// Exchange rate API (cached 10 minutes)
builder.Services.AddScoped<TrainingCenterManagement_MVC.Services.ExchangeRateApiService>();

// ====================== Build App ======================
var app = builder.Build();

// Static files for uploads folder with correct MIME types for audio/video
var uploadsContentTypeProvider = new FileExtensionContentTypeProvider();
uploadsContentTypeProvider.Mappings[".webm"] = "audio/webm";
uploadsContentTypeProvider.Mappings[".ogg"]  = "audio/ogg";
uploadsContentTypeProvider.Mappings[".mp3"]  = "audio/mpeg";
uploadsContentTypeProvider.Mappings[".wav"]  = "audio/wav";
uploadsContentTypeProvider.Mappings[".mp4"]  = "video/mp4";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(app.Environment.WebRootPath, "uploads")),
    RequestPath = "/uploads",
    ContentTypeProvider = uploadsContentTypeProvider,
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.Append("Cache-Control", "private, max-age=3600")
});

// Seed Data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    //await context.Database.MigrateAsync();
    var roleInitializer = services.GetRequiredService<RoleInitializer>();
    await roleInitializer.SeedRolesAsync();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var seeder = new SeedDataInitializer(context, userManager);

    // Run the seeder on every startup. Seed methods are idempotent and this
    // lets existing databases receive new modules like online exams.
    await seeder.SeedAllAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRotativa();

// Language middleware
// CurrentUICulture → controls UI string resources (Arabic/English labels).
// CurrentCulture   → controls date/number formatting. We always use en-US here
//                    to avoid UmAlQuraCalendar range errors (valid only 1900–2077).
app.Use(async (context, next) =>
{
    var formatCulture = new CultureInfo("en-US"); // safe Gregorian calendar for formatting
    if (context.Request.Cookies.TryGetValue("Language", out var langCookie))
    {
        Thread.CurrentThread.CurrentCulture   = formatCulture;
        Thread.CurrentThread.CurrentUICulture = new CultureInfo(langCookie);
    }
    else
    {
        Thread.CurrentThread.CurrentCulture   = formatCulture;
        Thread.CurrentThread.CurrentUICulture = new CultureInfo("ar-SA");
    }
    await next();
});

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.StatusCode == 403)
        response.Redirect("/Account/AccessDenied");
    else if (response.StatusCode == 404)
        response.Redirect("/Home/Error404");
});

app.UseRouting();

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
