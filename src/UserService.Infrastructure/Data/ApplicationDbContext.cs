using Microsoft.EntityFrameworkCore;
using UserService.Domain.Entities;

namespace UserService.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Company> Companies { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("Companies");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Role).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Department).HasMaxLength(255);
            entity.Property(e => e.CompanyId).IsRequired(false);
            entity.Property(e => e.ManagerId).IsRequired(false);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.SetNull);
            // ManagerIdは同じUsersテーブルへの自己参照（直属の上司）。
            // 入社手続きの登録漏れ等でNULLになり得るため、SetNullで整合性を保つ。
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .HasConstraintName("FK_Users_Users_ManagerId")
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}

