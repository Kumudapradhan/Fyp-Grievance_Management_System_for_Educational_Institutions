using GMS.Web.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GMS.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Subcategory> Subcategories { get; set; } = null!;
        public DbSet<Grievance> Grievances { get; set; } = null!;
        public DbSet<GrievanceAttachment> GrievanceAttachments { get; set; } = null!;
        public DbSet<GrievanceStatusHistory> GrievanceStatusHistories { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;
        public DbSet<Comment> Comments { get; set; } = null!;
        public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Grievance
            modelBuilder.Entity<Grievance>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.HasIndex(g => g.TicketNumber).IsUnique();

                // Performance Indexes
                entity.HasIndex(g => g.CategoryId);
                entity.HasIndex(g => g.SubcategoryId);
                entity.HasIndex(g => g.DepartmentId);
                entity.HasIndex(g => g.StudentId);
                entity.HasIndex(g => g.AssignedStaffUserId);
                entity.HasIndex(g => g.Status);
                entity.HasIndex(g => g.SubmittedAt);

                entity.HasOne(g => g.Category)
                    .WithMany()
                    .HasForeignKey(g => g.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(g => g.Subcategory)
                    .WithMany()
                    .HasForeignKey(g => g.SubcategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(g => g.Department)
                    .WithMany()
                    .HasForeignKey(g => g.DepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(g => g.Student)
                    .WithMany()
                    .HasForeignKey(g => g.StudentId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(g => g.AssignedStaffUser)
                    .WithMany()
                    .HasForeignKey(g => g.AssignedStaffUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Department
            modelBuilder.Entity<Department>(entity =>
            {
                entity.HasKey(d => d.Id);
                
                entity.HasOne(d => d.StaffUser)
                    .WithMany()
                    .HasForeignKey(d => d.StaffUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(c => c.Id);

                entity.HasOne(c => c.DefaultDepartment)
                    .WithMany()
                    .HasForeignKey(c => c.DefaultDepartmentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure GrievanceAttachment
            modelBuilder.Entity<GrievanceAttachment>(entity =>
            {
                entity.HasKey(ga => ga.Id);

                entity.HasOne(ga => ga.Grievance)
                    .WithMany(g => g.Attachments)
                    .HasForeignKey(ga => ga.GrievanceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure GrievanceStatusHistory
            modelBuilder.Entity<GrievanceStatusHistory>(entity =>
            {
                entity.HasKey(gsh => gsh.Id);
                entity.HasIndex(gsh => gsh.GrievanceId);
                entity.HasIndex(gsh => gsh.ChangedByUserId);

                entity.HasOne(gsh => gsh.Grievance)
                    .WithMany(g => g.StatusHistory)
                    .HasForeignKey(gsh => gsh.GrievanceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(gsh => gsh.ChangedByUser)
                    .WithMany()
                    .HasForeignKey(gsh => gsh.ChangedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Notification
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(n => n.Id);
                entity.HasIndex(n => n.UserId);
                entity.HasIndex(n => n.IsRead);

                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(n => n.Grievance)
                    .WithMany()
                    .HasForeignKey(n => n.GrievanceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.HasKey(al => al.Id);
                entity.HasIndex(al => al.Timestamp);
                entity.HasIndex(al => al.UserId);

                entity.HasOne(al => al.User)
                    .WithMany()
                    .HasForeignKey(al => al.UserId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Subcategory
            modelBuilder.Entity<Subcategory>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Name).IsRequired().HasMaxLength(100);

                entity.HasOne(s => s.Category)
                    .WithMany()
                    .HasForeignKey(s => s.CategoryId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configure Comment
            modelBuilder.Entity<Comment>(entity =>
            {
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Content).IsRequired().HasMaxLength(2000);

                entity.HasOne(c => c.Grievance)
                    .WithMany(g => g.Comments)
                    .HasForeignKey(c => c.GrievanceId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.User)
                    .WithMany()
                    .HasForeignKey(c => c.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure SystemSetting
            modelBuilder.Entity<SystemSetting>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.HasIndex(s => s.Key).IsUnique();
                entity.Property(s => s.Key).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Value).IsRequired().HasMaxLength(1000);
            });
        }
    }
}
