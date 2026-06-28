using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogCollectorApp.Models;

[Table("log_sources", Schema = "pm02")]
public class LogSource
{
    [Key, Column("id")]
    public long Id { get; set; }

    [Required, Column("group_id")]
    public long GroupId { get; set; }

    [Required, Column("log_path"), MaxLength(500)]
    public string LogPath { get; set; } = string.Empty;

    [Column("archive_path"), MaxLength(500)]
    public string? ArchivePath { get; set; }

    [Required, Column("file_mask"), MaxLength(100)]
    public string FileMask { get; set; } = string.Empty;

    [Required, Column("search_mask"), MaxLength(200)]
    public string SearchMask { get; set; } = string.Empty;

    [Column("timestamp_format"), MaxLength(50)]
    public string? TimestampFormat { get; set; }

    [Column("encoding"), MaxLength(20)]
    public string Encoding { get; set; } = "UTF-8";

    [ForeignKey("GroupId")]
    public ServerGroup? Group { get; set; }
}
