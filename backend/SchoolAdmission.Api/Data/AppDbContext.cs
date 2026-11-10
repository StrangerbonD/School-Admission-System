using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data.Entities;

namespace SchoolAdmission.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<HoldEntity> Holds => Set<HoldEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ClassEntity>(entity =>
        {
            entity.ToTable("classes", t =>
            {
                t.HasCheckConstraint("CK_classes_total_seats", "total_seats > 0");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();
            entity.Property(e => e.TotalSeats)
                .HasColumnName("total_seats")
                .IsRequired();
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()")
                .IsRequired();
        });

        modelBuilder.Entity<HoldEntity>(entity =>
        {
            entity.ToTable("holds", t =>
            {
                t.HasCheckConstraint("CK_holds_status", "status IN ('HELD','CONFIRMED','EXPIRED','CANCELLED')");
            });

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ClassId)
                .HasColumnName("class_id")
                .IsRequired();
            entity.Property(e => e.StudentId)
                .HasColumnName("student_id")
                .HasMaxLength(17)
                .IsRequired();
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .IsRequired();
            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at")
                .HasColumnType("timestamptz")
                .IsRequired();
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .HasDefaultValueSql("now()")
                .IsRequired();

            entity.HasOne(e => e.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(e => e.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ClassId, e.StudentId })
                .HasDatabaseName("uq_active_hold")
                .IsUnique()
                .HasFilter("status IN ('HELD','CONFIRMED')");

            entity.HasIndex(e => new { e.ClassId, e.Status, e.ExpiresAt })
                .HasDatabaseName("ix_holds_class_status_expires");

            entity.HasIndex(e => new { e.StudentId, e.CreatedAt })
                .HasDatabaseName("ix_holds_student_created")
                .IsDescending(false, true);
        });
    }
}
