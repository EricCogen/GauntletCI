using Microsoft.EntityFrameworkCore;
using GauntletCI.Watchtower.Models.Entities;

namespace GauntletCI.Watchtower.Data;

public class WatchtowerDbContext : DbContext
{
    public WatchtowerDbContext(DbContextOptions<WatchtowerDbContext> options) : base(options)
    {
    }

    public DbSet<GauntletRun> GauntletRuns { get; set; } = null!;
    public DbSet<CVEFeed> CVEFeeds { get; set; } = null!;
    public DbSet<CVEFeedEntry> CVEFeedEntries { get; set; } = null!;
    public DbSet<CVE> CVEs { get; set; } = null!;
    public DbSet<TechnicalData> TechnicalData { get; set; } = null!;
    public DbSet<ValidationRun> ValidationRuns { get; set; } = null!;
    public DbSet<ValidationResult> ValidationResults { get; set; } = null!;
    public DbSet<Article> Articles { get; set; } = null!;
    public DbSet<ArticleDraft> ArticleDrafts { get; set; } = null!;
    public DbSet<Miss> Misses { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure relationships
        modelBuilder.Entity<CVEFeed>()
            .HasMany(f => f.Entries)
            .WithOne(e => e.Feed)
            .HasForeignKey(e => e.FeedId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CVE>()
            .HasOne(c => c.TechnicalData)
            .WithOne(t => t.CVE)
            .HasForeignKey<TechnicalData>(t => t.CveId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CVE>()
            .HasMany(c => c.ValidationResults)
            .WithOne(vr => vr.CVE)
            .HasForeignKey(vr => vr.CveId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValidationRun>()
            .HasOne(vr => vr.GauntletRun)
            .WithMany()
            .HasForeignKey(vr => vr.GauntletRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ValidationRun>()
            .HasMany(vr => vr.Results)
            .WithOne(r => r.ValidationRun)
            .HasForeignKey(r => r.ValidationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Create unique indexes
        modelBuilder.Entity<CVE>()
            .HasIndex(c => c.CveId)
            .IsUnique();

        modelBuilder.Entity<CVEFeedEntry>()
            .HasIndex(e => new { e.FeedId, e.CveId })
            .IsUnique();

        // Configure enum conversions
        modelBuilder.Entity<CVEFeedEntry>()
            .Property(e => e.Status)
            .HasConversion<string>();

        modelBuilder.Entity<ValidationRun>()
            .Property(vr => vr.Status)
            .HasConversion<string>();

        modelBuilder.Entity<TechnicalData>()
            .Property(td => td.VulnerabilityClass)
            .HasConversion<string>();

        modelBuilder.Entity<Alert>()
            .Property(a => a.Type)
            .HasConversion<string>();

        modelBuilder.Entity<Alert>()
            .Property(a => a.Severity)
            .HasConversion<string>();
    }
}
