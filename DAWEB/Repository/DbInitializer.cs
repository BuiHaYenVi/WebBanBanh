using DAWEB.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DAWEB.Repository
{
    public static class DbInitializer
    {
        public static void Seed(DataContext context)
        {
            if (!context.Database.CanConnect())
            {
                // Database does not exist yet. Run Migrate() directly to create it and apply migrations.
                context.Database.Migrate();
            }
            else
            {
                // Tạo bảng lịch sử migration nếu chưa có và chèn các migration cũ để tránh lỗi trùng lặp bảng khi chạy Migrate()
                // Chỉ chèn nếu các bảng tương ứng đã tồn tại trong database (để tránh lỗi khi xóa DB và tạo lại từ đầu).
                context.Database.ExecuteSqlRaw(@"
                    IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
                    BEGIN
                        CREATE TABLE [__EFMigrationsHistory] (
                            [MigrationId] nvarchar(150) NOT NULL,
                            [ProductVersion] nvarchar(32) NOT NULL,
                            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
                        );
                    END;

                    IF OBJECT_ID(N'[dbo].[Products]') IS NOT NULL
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260309162537_InitProductModel')
                            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20260309162537_InitProductModel', '9.0.2');
                    END;

                    IF OBJECT_ID(N'[dbo].[AspNetUsers]') IS NOT NULL
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260310145433_InitIdentity')
                            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20260310145433_InitIdentity', '9.0.2');
                    END;

                    IF OBJECT_ID(N'[dbo].[Products]') IS NOT NULL AND EXISTS(SELECT * FROM sys.columns WHERE Name = N'CreateDate' AND Object_ID = OBJECT_ID(N'[dbo].[Products]'))
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260312090052_CapNhat_datetime_choSP')
                            INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) VALUES ('20260312090052_CapNhat_datetime_choSP', '9.0.2');
                    END;
                ");

                // Chạy Migrate() để tự động áp dụng các migration mới (như bảng Orders và OrderDetails)
                context.Database.Migrate();
            }

            // Seed Categories if empty
            if (!context.Categories.Any())
            {
                var categories = new CategoryModel[]
                {
                    new CategoryModel { Id = 1, Name = "Bánh Kem", Slug = "banhkem", Description = "Bánh Kem / Sinh Nhật" },
                    new CategoryModel { Id = 2, Name = "Bánh Ngọt", Slug = "banhngot", Description = "Bánh Ngọt / Pastry" },
                    new CategoryModel { Id = 3, Name = "Bánh Mì", Slug = "banhmi", Description = "Bánh Mì / Bread" }
                };

                var isSqlServer = context.Database.IsSqlServer();
                if (isSqlServer)
                {
                    context.Database.OpenConnection();
                    try
                    {
                        context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Categories ON");
                        context.Categories.AddRange(categories);
                        context.SaveChanges();
                        context.Database.ExecuteSqlRaw("SET IDENTITY_INSERT Categories OFF");
                    }
                    finally
                    {
                        context.Database.CloseConnection();
                    }
                }
                else
                {
                    context.Categories.AddRange(categories);
                    context.SaveChanges();
                }
            }

            // Chỉ nạp dữ liệu sản phẩm mặc định nếu bảng sản phẩm đang hoàn toàn trống.
            if (!context.Products.Any())
            {

                // Nạp danh sách bánh ngọt mới
                var products = new ProductModels[]
                {
                    // CategoryId 1: Bánh Kem / Sinh Nhật
                    new ProductModels
                    {
                        ProductName = "Bánh Kem Dâu Tây Hoàng Gia",
                        ProductDescription = "Bánh kem sinh nhật cốt bông lan vani xen kẽ mứt dâu tây tươi và kem tươi béo ngậy, trang trí dâu tươi chín mọng nhập khẩu.",
                        ProductPrice = 350000,
                        ProductColor = "Dâu Tây & Vani",
                        ProductSize = "Đường kính 20cm",
                        ProductMaterial = "Kem tươi Pháp, dâu tây Đà Lạt, cốt bánh vani",
                        ProductGender = "Bánh Kem",
                        ProductQuantity = 15,
                        CategoryId = 1,
                        ImageUrl = "https://images.unsplash.com/photo-1578985545062-69928b1d9587?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },
                    new ProductModels
                    {
                        ProductName = "Bánh Tiramisu Truyền Thống Ý",
                        ProductDescription = "Sự kết hợp hoàn hảo giữa phô mai Mascarpone béo ngậy, cà phê Espresso thơm lừng và rượu rum nồng nàn phủ bột cacao.",
                        ProductPrice = 280000,
                        ProductColor = "Cà phê & Cacao",
                        ProductSize = "Đường kính 18cm",
                        ProductMaterial = "Phô mai Mascarpone, cà phê Espresso, bánh sampa",
                        ProductGender = "Bánh Kem",
                        ProductQuantity = 20,
                        CategoryId = 1,
                        ImageUrl = "https://images.unsplash.com/photo-1571877227200-a0d98ea607e9?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },
                    new ProductModels
                    {
                        ProductName = "Bánh Mousse Chanh Leo Thanh Mát",
                        ProductDescription = "Lớp mousse chanh leo chua nhẹ, mát lạnh kết hợp với đế bánh quy giòn tan, thích hợp cho những buổi trà chiều.",
                        ProductPrice = 260000,
                        ProductColor = "Chanh leo vàng",
                        ProductSize = "Đường kính 18cm",
                        ProductMaterial = "Nước cốt chanh leo tươi, kem whipping, gelatin Pháp",
                        ProductGender = "Bánh Kem",
                        ProductQuantity = 10,
                        CategoryId = 1,
                        ImageUrl = "https://images.unsplash.com/photo-1534432127792-62f928d4479e?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },

                    // CategoryId 2: Bánh Ngọt / Pastry
                    new ProductModels
                    {
                        ProductName = "Hộp Bánh Macaron Pháp Sặc Sỡ (6 cái)",
                        ProductDescription = "Bánh macaron trứ danh nước Pháp lớp vỏ giòn tan, nhân kem chocolate ngọt ngào với nhiều hương vị tự nhiên.",
                        ProductPrice = 120000,
                        ProductColor = "Nhiều màu sắc",
                        ProductSize = "Hộp 6 bánh",
                        ProductMaterial = "Bột hạnh nhân, lòng trắng trứng, kem ganache socola",
                        ProductGender = "Bánh Ngọt",
                        ProductQuantity = 30,
                        CategoryId = 2,
                        ImageUrl = "https://images.unsplash.com/photo-1569864358642-9d1684040f43?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },
                    new ProductModels
                    {
                        ProductName = "Bánh Cupcake Socola Đậm Vị",
                        ProductDescription = "Bánh cupcake socola xốp mềm kết hợp cùng lớp kem bơ socola đắng nhẹ, trang trí thêm cốm màu bắt mắt.",
                        ProductPrice = 35000,
                        ProductColor = "Socola đậm",
                        ProductSize = "Cỡ vừa (Medium)",
                        ProductMaterial = "Bột cacao nguyên chất, kem bơ socola, sữa tươi",
                        ProductGender = "Bánh Ngọt",
                        ProductQuantity = 50,
                        CategoryId = 2,
                        ImageUrl = "https://images.unsplash.com/photo-1576618148400-f54bed99fcfd?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },
                    new ProductModels
                    {
                        ProductName = "Bánh Donut Phủ Kem Dâu Đường Bột",
                        ProductDescription = "Bánh donut chiên phồng xốp mềm, phủ một lớp sốt kem dâu ngọt lịm cùng kẹo cốm trang trí.",
                        ProductPrice = 28000,
                        ProductColor = "Hồng ngọt ngào",
                        ProductSize = "Cỡ vừa (Medium)",
                        ProductMaterial = "Bột mì kem, sữa tươi, men bánh mì Pháp",
                        ProductGender = "Bánh Ngọt",
                        ProductQuantity = 40,
                        CategoryId = 2,
                        ImageUrl = "https://images.unsplash.com/photo-1551024601-bec78aea704b?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },

                    // CategoryId 3: Bánh Mì / Bread
                    new ProductModels
                    {
                        ProductName = "Bánh Sừng Bò (Croissant) Bơ Pháp",
                        ProductDescription = "Bánh sừng bò ngàn lớp chuẩn vị Pháp thơm nức mũi hương bơ cao cấp, giòn tan bên ngoài và mềm xốp bên trong.",
                        ProductPrice = 45000,
                        ProductColor = "Vàng óng",
                        ProductSize = "Cỡ lớn (Large)",
                        ProductMaterial = "Bột mì Pháp, bơ nhạt Anchor",
                        ProductGender = "Bánh Mì",
                        ProductQuantity = 35,
                        CategoryId = 3,
                        ImageUrl = "https://images.unsplash.com/photo-1555507036-ab1f4038808a?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },
                    new ProductModels
                    {
                        ProductName = "Bánh Mì Gối Ngũ Cốc Nguyên Cám",
                        ProductDescription = "Bánh mì gối ngũ cốc giàu chất xơ, thích hợp cho người ăn kiêng, tập gym, được nướng vàng đều thơm phức.",
                        ProductPrice = 65000,
                        ProductColor = "Nâu ngũ cốc",
                        ProductSize = "Ổ lớn 450g",
                        ProductMaterial = "Bột mì nguyên cám, hạt chia, hạt bí, yến mạch",
                        ProductGender = "Bánh Mì",
                        ProductQuantity = 15,
                        CategoryId = 3,
                        ImageUrl = "https://images.unsplash.com/photo-1509440159596-0249088772ff?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    },
                    new ProductModels
                    {
                        ProductName = "Bánh Mì Bơ Tỏi Phô Mai Hàn Quốc",
                        ProductDescription = "Bánh mì tròn giòn vỏ rưới đẫm sốt bơ tỏi ngọt dịu xen kẽ kem cheese Mascarpone béo ngậy tan chảy.",
                        ProductPrice = 75000,
                        ProductColor = "Vàng bơ tỏi",
                        ProductSize = "Đường kính 12cm",
                        ProductMaterial = "Bơ lạt, tỏi lý sơn, cream cheese Mascarpone",
                        ProductGender = "Bánh Mì",
                        ProductQuantity = 25,
                        CategoryId = 3,
                        ImageUrl = "https://images.unsplash.com/photo-1607958996333-41aef7caefaa?w=800",
                        CreateDate = DateTime.Now,
                        UpdateDate = DateTime.Now
                    }
                };

                context.Products.AddRange(products);
                context.SaveChanges();
            }
        }

        public static async Task SeedUsersAndRoles(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Seed Admin role if not exists
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }
            
            // Seed User role if not exists
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }

            // Seed user with UserName "admin" if not exists
            var adminUser = await userManager.FindByNameAsync("admin");
            if (adminUser == null)
            {
                adminUser = new IdentityUser
                {
                    UserName = "admin",
                    Email = "admin@gmail.com",
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(adminUser, "Admin@123");
                if (createResult.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
            else
            {
                // Ensure the existing admin user has the Admin role
                if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }
    }
}
