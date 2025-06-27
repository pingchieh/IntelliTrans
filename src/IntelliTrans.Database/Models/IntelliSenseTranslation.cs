using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IntelliTrans.Database.Models;

public class IntelliSenseTranslation
{
    public int Id { get; set; }

    [MaxLength(32)]
    public required string OriginalHash { get; set; }

    [Column(TypeName = "text")]
    public required string Content { get; set; }

    [MaxLength(50)]
    public required string Language { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public IntelliSenseOriginal Original { get; set; } = null!;
}
