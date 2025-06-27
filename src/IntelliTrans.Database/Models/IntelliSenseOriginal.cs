using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IntelliTrans.Database.Models;

public class IntelliSenseOriginal
{
    public int Id { get; set; }

    [MaxLength(32)]
    public required string Hash { get; set; }

    [Column(TypeName = "text")]
    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<IntelliSenseTranslation> Translations { get; set; } = null!;
}
