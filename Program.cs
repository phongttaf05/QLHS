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

// Đảm bảo bảng chuyên môn giáo viên (giao_vien_mon_hoc) tồn tại trong SQL Server
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<QlhsContext>();
        db.Database.ExecuteSqlRaw(@"
            IF OBJECT_ID(N'dbo.giao_vien_mon_hoc', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.giao_vien_mon_hoc (
                    giao_vien_id INT NOT NULL,
                    mon_hoc_id INT NOT NULL,
                    CONSTRAINT PK_giao_vien_mon_hoc PRIMARY KEY (giao_vien_id, mon_hoc_id),
                    CONSTRAINT FK_gvmh_giao_vien FOREIGN KEY (giao_vien_id) REFERENCES dbo.giao_vien(id) ON DELETE CASCADE,
                    CONSTRAINT FK_gvmh_mon_hoc FOREIGN KEY (mon_hoc_id) REFERENCES dbo.mon_hoc(id) ON DELETE CASCADE
                );
                INSERT INTO dbo.giao_vien_mon_hoc (giao_vien_id, mon_hoc_id)
                SELECT DISTINCT giao_vien_id, mon_hoc_id
                FROM dbo.phan_cong_giang_day
                WHERE giao_vien_id IS NOT NULL AND mon_hoc_id IS NOT NULL;
            END
            IF OBJECT_ID(N'dbo.ban_tkb_xac_nhan_gv', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ban_tkb_xac_nhan_gv (
                    ban_tkb_id INT NOT NULL,
                    giao_vien_id INT NOT NULL,
                    trang_thai VARCHAR(24) NOT NULL DEFAULT 'CHO_XAC_NHAN',
                    y_kien NVARCHAR(500) NULL,
                    ngay_phan_hoi DATETIME2(0) NULL,
                    CONSTRAINT PK_ban_tkb_xac_nhan_gv PRIMARY KEY (ban_tkb_id, giao_vien_id),
                    CONSTRAINT FK_tkbxn_ban_tkb FOREIGN KEY (ban_tkb_id) REFERENCES dbo.ban_tkb(id) ON DELETE CASCADE,
                    CONSTRAINT FK_tkbxn_giao_vien FOREIGN KEY (giao_vien_id) REFERENCES dbo.giao_vien(id) ON DELETE CASCADE
                );
            END");
    }
    catch { }
}

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
