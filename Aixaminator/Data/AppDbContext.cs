using Microsoft.EntityFrameworkCore;

namespace Aixaminator.Data;

public class AppDbContext : DbContext
{
    public DbSet<Document> Documents { get; set; }
    public DbSet<DocumentPart> DocumentParts { get; set; }
    public DbSet<Note> Notes { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> options)
    : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DocumentPart>()
            .HasKey(dp => new { dp.DocumentId, dp.PartNumber });

        modelBuilder.Entity<Document>()
            .HasMany(d => d.Notes)
            .WithOne(n => n.Document)
            .HasForeignKey(n => n.DocumentId);

        modelBuilder.Entity<Note>()
            .HasOne(n => n.Part)
            .WithMany(dp => dp.Notes)
            .HasForeignKey(n => new { n.DocumentId, n.DocumentPartNumber })
            .IsRequired(false);

        modelBuilder.ApplyConfiguration(new NotesConfiguration());
    }
}
