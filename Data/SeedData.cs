using GRP_03_27.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = serviceProvider.GetRequiredService<UserManager<User>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Ensure database is created
            await context.Database.EnsureCreatedAsync();

            // Seed roles
            string[] roleNames = {
                "Administrator",
                "CustomerLiaison",
                "InventoryLiaison",
                "FaultTechnician",
                "MaintenanceTechnician",
                "PurchasingManager",
                "Customer"  // For customers who register themselves
            };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // Seed admin user
            var adminUser = await userManager.FindByEmailAsync("admin@fridgemanagement.com");
            if (adminUser == null)
            {
                adminUser = new User
                {
                    UserName = "admin@fridgemanagement.com",
                    Email = "admin@fridgemanagement.com",
                    FirstName = "System",
                    LastName = "Administrator",
                    EmailConfirmed = true
                };

                var createPowerUser = await userManager.CreateAsync(adminUser, "Admin123!");
                if (createPowerUser.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Administrator");

                    // Store admin password in history
                    var passwordHash = userManager.PasswordHasher.HashPassword(adminUser, "Admin123!");
                    context.PasswordHistories.Add(new PasswordHistory
                    {
                        UserId = adminUser.Id,
                        PasswordHash = passwordHash,
                        CreatedDate = DateTime.UtcNow
                    });

                    Console.WriteLine("Created Administrator: admin@fridgemanagement.com");
                }
            }

            // Seed Customer Liaison
            await SeedEmployee(userManager, context, "liaison@fridgemanagement.com", "Sarah", "Johnson", "CustomerLiaison", "Liaison123!");

            // Seed Inventory Liaison
            await SeedEmployee(userManager, context, "inventory@fridgemanagement.com", "Mike", "Wilson", "InventoryLiaison", "Inventory123!");

            // Seed Fault Technician
            await SeedEmployee(userManager, context, "technician@fridgemanagement.com", "David", "Brown", "FaultTechnician", "Tech123!");

            // Seed Maintenance Technician
            await SeedEmployee(userManager, context, "maintenance@fridgemanagement.com", "Lisa", "Davis", "MaintenanceTechnician", "Maintenance123!");

            // Seed Purchasing Manager
            await SeedEmployee(userManager, context, "purchasing@fridgemanagement.com", "James", "Miller", "PurchasingManager", "Purchasing123!");

            // Seed sample supplier (for testing purchase requests)
            if (!context.Suppliers.Any())
            {
                context.Suppliers.Add(new Supplier
                {
                    Name = "CoolFridge Suppliers",
                    ContactNumber = "+27111234567",
                    Email = "info@coolfridge.co.za",
                    Address = "123 Supplier Street, Johannesburg, South Africa"
                });

                context.Suppliers.Add(new Supplier
                {
                    Name = "Fridge Masters",
                    ContactNumber = "+27119876543",
                    Email = "sales@fridgemasters.co.za",
                    Address = "456 Cool Avenue, Cape Town, South Africa"
                });
                await context.SaveChangesAsync();

                Console.WriteLine("Created sample suppliers");
            }

            // Seed sample fridges (for testing allocation)
            if (!context.Fridges.Any())
            {
                var supplier = await context.Suppliers.FirstAsync();

                context.Fridges.AddRange(
                    new Fridge
                    {
                        Model = "CoolMaster Pro 500",
                        SerialNumber = "CM500-001",
                        PurchaseDate = DateTime.Now.AddMonths(-6),
                        Status = GRP_03_27.Enums.FridgeStatus.Available,
                        SupplierId = supplier.SupplierId
                    },
                    new Fridge
                    {
                        Model = "FridgeKing Ultra",
                        SerialNumber = "FKU-002",
                        PurchaseDate = DateTime.Now.AddMonths(-3),
                        Status = GRP_03_27.Enums.FridgeStatus.Available,
                        SupplierId = supplier.SupplierId
                    },
                    new Fridge
                    {
                        Model = "IceCool Deluxe",
                        SerialNumber = "ICD-003",
                        PurchaseDate = DateTime.Now.AddMonths(-1),
                        Status = GRP_03_27.Enums.FridgeStatus.Available,
                        SupplierId = supplier.SupplierId
                    }
                );
                await context.SaveChangesAsync();

                Console.WriteLine("Created sample fridges");
            }

            Console.WriteLine("Database seeding completed successfully!");
        }

        private static async Task SeedEmployee(UserManager<User> userManager, ApplicationDbContext context,
            string email, string firstName, string lastName, string role, string password)
        {
            var employee = await userManager.FindByEmailAsync(email);
            if (employee == null)
            {
                employee = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(employee, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(employee, role);

                    // Store password in history
                    var passwordHash = userManager.PasswordHasher.HashPassword(employee, password);
                    context.PasswordHistories.Add(new PasswordHistory
                    {
                        UserId = employee.Id,
                        PasswordHash = passwordHash,
                        CreatedDate = DateTime.UtcNow
                    });
                    await context.SaveChangesAsync();

                    Console.WriteLine($"Created {role}: {email}");
                }
                else
                {
                    Console.WriteLine($"Failed to create {role}: {email}");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Error: {error.Description}");
                    }
                }
            }
        }
        private static async Task SeedCustomer(UserManager<User> userManager, ApplicationDbContext context,
        string email, string firstName, string lastName, string role, string password)
        {
            var customerUser = await userManager.FindByEmailAsync(email);
            if (customerUser == null)
            {
                customerUser = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(customerUser, password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(customerUser, role);

                    // Create customer record linked to the user
                    var customer = new Customer
                    {
                        UserId = customerUser.Id, // This is the crucial link
                        BusinessName = $"{firstName} {lastName} Enterprises",
                        ContactPerson = $"{firstName} {lastName}",
                        PhoneNumber = "+27110000000",
                        Email = email,
                        PhysicalAddress = "123 Customer Street, South Africa",
                        Type = GRP_03_27.Enums.CustomerType.SpazaShop,
                        RegistrationDate = DateTime.UtcNow,
                        LastUpdated = DateTime.UtcNow,
                        IsActive = true
                    };

                    context.Customers.Add(customer);

                    // Store password in history
                    var passwordHash = userManager.PasswordHasher.HashPassword(customerUser, password);
                    context.PasswordHistories.Add(new PasswordHistory
                    {
                        UserId = customerUser.Id,
                        PasswordHash = passwordHash,
                        CreatedDate = DateTime.UtcNow
                    });

                    await context.SaveChangesAsync();

                    Console.WriteLine($"Created Customer: {email} with password: {password}");
                }
            }
        }
    }
}