using IDADRS.Core.Entities;
using IDADRS.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IDADRS.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User>         Users         => Set<User>();
    public DbSet<Document>     Documents     => Set<Document>();
    public DbSet<Category>     Categories    => Set<Category>();
    public DbSet<AccessLog>    AccessLogs    => Set<AccessLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ── Users ────────────────────────────────────────────────────────────
        b.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Username).HasMaxLength(60).IsRequired();
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role)
             .HasConversion(new EnumToStringConverter<UserRole>())
             .HasMaxLength(20);
            e.Property(u => u.CreatedDate).HasDefaultValueSql("NOW()");
        });

        // ── Categories ────────────────────────────────────────────────────────
        b.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(c => c.Id);
            e.Property(c => c.CategoryName).HasMaxLength(100).IsRequired();
            e.Property(c => c.Description).HasMaxLength(500);
        });

        // ── Documents ─────────────────────────────────────────────────────────
        b.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.Title).HasMaxLength(200).IsRequired();
            e.Property(d => d.Description).HasMaxLength(2000);
            e.Property(d => d.FilePath).IsRequired();
            e.Property(d => d.UploadDate).HasDefaultValueSql("NOW()");

            e.HasOne(d => d.Uploader)
             .WithMany(u => u.Documents)
             .HasForeignKey(d => d.UploadedBy)
             .OnDelete(DeleteBehavior.SetNull);

            e.HasOne(d => d.Category)
             .WithMany(c => c.Documents)
             .HasForeignKey(d => d.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            // Indexes
            e.HasIndex(d => d.Title);
            e.HasIndex(d => d.CategoryId);

            // PostgreSQL tsvector full-text search column (GIN index)
            e.HasGeneratedTsVectorColumn(
                d => d.SearchVector,
                "english",
                d => new { d.Title, d.Description })
             .HasIndex(d => d.SearchVector)
             .HasMethod("GIN");
        });

        // ── AccessLogs ────────────────────────────────────────────────────────
        b.Entity<AccessLog>(e =>
        {
            e.ToTable("access_logs");
            e.HasKey(a => a.Id);
            e.Property(a => a.ActionType).HasMaxLength(20).IsRequired();
            e.Property(a => a.AccessDate).HasDefaultValueSql("NOW()");

            e.HasOne(a => a.User)
             .WithMany(u => u.AccessLogs)
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.Document)
             .WithMany(d => d.AccessLogs)
             .HasForeignKey(a => a.DocumentId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(a => new { a.UserId, a.AccessDate });
        });

        // ── RefreshTokens ─────────────────────────────────────────────────────
        b.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(t => t.Id);
            e.Property(t => t.Token).IsRequired();
            e.HasIndex(t => t.Token).IsUnique();

            e.HasOne(t => t.User)
             .WithMany(u => u.Tokens)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
