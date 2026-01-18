using GRP_03_27.Data;
using GRP_03_27.Models;
using GRP_03_27.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27
{
    public class Program
    {
        public static async Task Main(string[] args) // Changed to async Task
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Add Entity Framework Core with SQL Server
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")));

            // Add ASP.NET Core Identity with custom User class
            builder.Services.AddIdentity<User, IdentityRole>(options =>
            {
                // Password settings
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireUppercase = true;
                options.Password.RequiredLength = 6;
                options.Password.RequiredUniqueChars = 1;

                // Lockout settings
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.AllowedForNewUsers = true;

                // User settings
                options.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                options.User.RequireUniqueEmail = true;

                // SignIn settings
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders()
            .AddDefaultUI();

            // Configure application cookie
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                options.LoginPath = "/Account/Login";
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.SlidingExpiration = true;
            });

            // Add session support
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Add authorization policies for role-based access
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("RequireCustomerLiaison", policy =>
                    policy.RequireRole("CustomerLiaison"));
                options.AddPolicy("RequireInventoryLiaison", policy =>
                    policy.RequireRole("InventoryLiaison"));
                options.AddPolicy("RequireFaultTechnician", policy =>
                    policy.RequireRole("FaultTechnician"));
                options.AddPolicy("RequireMaintenanceTechnician", policy =>
                    policy.RequireRole("MaintenanceTechnician"));
                options.AddPolicy("RequirePurchasingManager", policy =>
                    policy.RequireRole("PurchasingManager"));
                options.AddPolicy("RequireAdmin", policy =>
                    policy.RequireRole("Administrator"));
            });

            // Add HTTP context accessor
            builder.Services.AddHttpContextAccessor();

            // Add notification service
            builder.Services.AddScoped<INotificationService, NotificationService>();

            // Add background notification service
            builder.Services.AddHostedService<NotificationBackgroundService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            else
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            // Add authentication & authorization middleware
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseSession();

            // Map Razor Pages for Identity UI
            app.MapRazorPages();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Seed initial data
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<ApplicationDbContext>();
                    var userManager = services.GetRequiredService<UserManager<User>>();
                    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                    // Ensure database is created and migrations are applied
                    await context.Database.EnsureCreatedAsync();

                    // Seed initial roles and admin user
                    await SeedData.Initialize(services);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred seeding the DB.");
                }
            }

            await app.RunAsync();
        }
    }
}