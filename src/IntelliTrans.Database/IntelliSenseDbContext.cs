using IntelliTrans.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace IntelliTrans.Database;

public class IntelliSenseDbContext : DbContext
{
    public DbSet<IntelliSenseOriginal> Originals { get; set; }
    public DbSet<IntelliSenseTranslation> Translations { get; set; }

    public IntelliSenseDbContext(DbContextOptions<IntelliSenseDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntelliSenseOriginal>().HasAlternateKey(e => e.Hash);
        modelBuilder
            .Entity<IntelliSenseTranslation>()
            .HasOne(t => t.Original)
            .WithMany(o => o.Translations)
            .HasForeignKey(t => t.OriginalHash)
            .HasPrincipalKey(o => o.Hash);
    }
}
