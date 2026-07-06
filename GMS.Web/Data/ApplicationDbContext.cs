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
        public DbSet<Grievance> Grievances { get; set; } = null!;
        public DbSet<GrievanceAttachment> GrievanceAttachments { get; set; } = null!;
        public DbSet<GrievanceStatusHistory> GrievanceStatusHistories { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Grievance
            modelBuilder.Entity<Grievance>(entity =>
            {
                entity.HasKey(g => g.Id);
                entity.HasIndex(g => g.TicketNumber).IsUnique();

                entity.HasOne(g => g.Category)
                    .WithMany()
                    .HasForeignKey(g => g.CategoryId)
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

                entity.HasOne(al => al.User)
                    .WithMany()
                    .HasForeignKey(al => al.UserId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
