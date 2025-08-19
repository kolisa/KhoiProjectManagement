using KhoiProjectManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Net.Mail;
using System.Reflection.Emit;
using Attachment = KhoiProjectManagement.Models.Attachment;

namespace KhoiProjectManagementApi.Data
{
    public class ProjectManagementContext : DbContext
    {
        public ProjectManagementContext(DbContextOptions<ProjectManagementContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<ProjectTask> Tasks { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Attachment> Attachments { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ProjectUser> ProjectUsers { get; set; }
        public DbSet<ProjectTag> ProjectTags { get; set; }
        public DbSet<TaskTag> TaskTags { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure composite keys
            modelBuilder.Entity<ProjectUser>()
                .HasKey(pu => new { pu.ProjectId, pu.UserId });

            modelBuilder.Entity<ProjectTag>()
                .HasKey(pt => new { pt.ProjectId, pt.TagId });

            modelBuilder.Entity<TaskTag>()
                .HasKey(tt => new { tt.TaskId, tt.TagId });

            // Configure relationships
            modelBuilder.Entity<Project>()
                .HasOne(p => p.Creator)
                .WithMany()
                .HasForeignKey(p => p.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ProjectTask>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Attachment>()
                .HasOne(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed data
            // Fix seed data - use fixed DateTime values and proper password hashing
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Name = "Kolisa Mjobo",
                    Email = "kholisa@khoitech.Africa",
                    Role = "admin",
                    Position = "Full stack Developer",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new User
                {
                    Id = 2,
                    Name = "Seati Moloi",
                    Email = "seati@khoitech.Africa",
                    Role = "manager",
                    Position = "Business Analyst",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("manager123"),
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new User
                {
                    Id = 3,
                    Name = "Kenneth Mothobi",
                    Email = "kenneth@khoitech.Africa",
                    Role = "member",
                    Position = "System Analyst",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new User
                {
                    Id = 4,
                    Name = "Thato",
                    Email = "thato@khoitech.Africa",
                    Role = "member",
                    Position = "Marketing Manager",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new User
                {
                    Id = 5,
                    Name = "Metsing",
                    Email = "metsing@khoitech.Africa",
                    Role = "member",
                    Position = "Finance Manager",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new User
                {
                    Id = 6,
                    Name = "Lebo",
                    Email = "Relebohile@khoitech.Africa",
                    Role = "member",
                    Position = "Client Support Manager",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("member123"),
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                }
            );

            // Add seed data for Tags
            modelBuilder.Entity<Tag>().HasData(
                new Tag { Id = 1, Name = "web", Color = "#3B82F6", CreatedAt = new DateTime(2024, 1, 1) },
                new Tag { Id = 2, Name = "mobile", Color = "#10B981", CreatedAt = new DateTime(2024, 1, 1) },
                new Tag { Id = 3, Name = "design", Color = "#8B5CF6", CreatedAt = new DateTime(2024, 1, 1) },
                new Tag { Id = 4, Name = "development", Color = "#F59E0B", CreatedAt = new DateTime(2024, 1, 1) },
                new Tag { Id = 5, Name = "urgent", Color = "#EF4444", CreatedAt = new DateTime(2024, 1, 1) }
            );
        }
    }
}
