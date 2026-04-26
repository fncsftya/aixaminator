using Aixaminator.Data;
using Microsoft.EntityFrameworkCore;

namespace Aixaminator.Features;

public static class DocumentParts
{
    public static async Task<DocumentPart> GetAsync(DbContext context, Guid documentId, int partId, bool shouldSave = true)
    {
        var part = await context.Set<DocumentPart>()
            .FirstOrDefaultAsync(dp => dp.DocumentId == documentId && dp.PartNumber == partId);

        if (part is null)
        {
            var newPart = new DocumentPart
            {
                DocumentId = documentId,
                PartNumber = partId
            };
            context.Add(newPart);
            if (shouldSave)
            {
                await context.SaveChangesAsync();
            }
            return newPart;
        }

        return part;
    }

    public static async Task<List<DocumentPart>> GetPartsForDocumentAsync(DbContext context, Guid documentId)
    {
        return await context.Set<DocumentPart>()
            .Where(dp => dp.DocumentId == documentId)
            .OrderBy(dp => dp.PartNumber)
            .ToListAsync();
    }
}
