using Microsoft.EntityFrameworkCore;

namespace Aixaminator.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentPart> DocumentParts => Set<DocumentPart>();

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(document =>
        {
            document.Property(d => d.Name).IsRequired();

            document.HasMany(d => d.Parts)
                .WithOne()
                .HasForeignKey(p => p.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            document.HasMany(d => d.Notes)
                .WithOne()
                .HasForeignKey(n => n.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentPart>(part =>
        {
            part.HasKey(p => new { p.DocumentId, p.PartNumber });
            part.Ignore(p => p.DisplayName);

            part.HasMany<Note>()
                .WithOne()
                .HasForeignKey(n => new { n.DocumentId, n.DocumentPartNumber })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Note>(note =>
        {
            note.Ignore(n => n.IsHighlight);
            note.Ignore(n => n.IsQuestion);
            note.Property(n => n.Kind).HasConversion<string>();
            note.OwnsOne(n => n.Location);
        });
    }
}
