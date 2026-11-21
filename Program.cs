using Microsoft.EntityFrameworkCore;
using QLHS.Models; // Đổi QLHS theo namespace dự án của bạn
using QLHS.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
var builder = WebApplication.CreateBuilder(args);
// 1. Cấu hình Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";         // Đường dẫn chuyển hướng khi chưa đăng nhập
        options.AccessDeniedPath = "/Account/AccessDenied"; // Đường dẫn khi không đủ quyền
        options.ExpireTimeSpan = TimeSpan.FromHours(8); // Cookie sống trong 8 giờ
    });


// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IAppLockService, AppLockService>();
builder.Services.AddScoped<ITimetableSolverService, TimetableSolverService>();
// 1. Đăng ký QlhsContext kết nối tới SQL Server
builder.Services.AddDbContext<QlhsContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
