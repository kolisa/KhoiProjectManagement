using KhoiProjectManagement.Models;
using KhoiProjectManagementApi.Data;
using Microsoft.EntityFrameworkCore;

namespace KhoiProjectManagementApi
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(ProjectManagementContext context)
        {
            // Check if users already exist
            if (await context.Users.AnyAsync())
            {
                return; // Database has been seeded
            }

            // Add users
            var users = new List<User>
        {
            new User
            {
                Name = "Kolisa Mjobo",
                Email = "kholisa@khoitech.Africa",
                Role = "admin",
                Position = "Full stack Developer",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Name = "Seati Moloi",
                Email = "seati@khoitech.Africa",
                Role = "manager",
                Position = "Business Analyst",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Name = "Kenneth Mothobi",
                Email = "kenneth@khoitech.Africa",
                Role = "member",
                Position = "System Analyst",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Name = "Thato",
                Email = "thato@khoitech.Africa",
                Role = "member",
                Position = "Marketing Manager",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Name = "Metsing",
                Email = "metsing@khoitech.Africa",
                Role = "member",
                Position = "Finance Manager",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new User
            {
                Name = "Lebo",
                Email = "Relebohile@khoitech.Africa",
                Role = "member",
                Position = "Client Support Manager",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            }
        };

            context.Users.AddRange(users);
            await context.SaveChangesAsync();

            // Add tags
            var tags = new List<Tag>
        {
            new Tag { Name = "web", Color = "#3B82F6", CreatedAt = DateTime.UtcNow },
            new Tag { Name = "mobile", Color = "#10B981", CreatedAt = DateTime.UtcNow },
            new Tag { Name = "design", Color = "#8B5CF6", CreatedAt = DateTime.UtcNow },
            new Tag { Name = "development", Color = "#F59E0B", CreatedAt = DateTime.UtcNow },
            new Tag { Name = "urgent", Color = "#EF4444", CreatedAt = DateTime.UtcNow }
        };

            context.Tags.AddRange(tags);
            await context.SaveChangesAsync();

            // Add sample projects
            var projects = new List<Project>
        {
            new Project
            {
                Name = "KhoiTech Website Redesign",
                Description = "Complete overhaul of company website with modern design",
                Status = "active",
                Priority = "high",
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.AddMonths(3).Date,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new Project
            {
                Name = "Mobile App Development",
                Description = "iOS and Android app for customer portal",
                Status = "active",
                Priority = "medium",
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.AddMonths(6).Date,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

            context.Projects.AddRange(projects);
            await context.SaveChangesAsync();
        }
    }
}
