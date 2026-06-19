using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogCollectorApp.Models
{
    /// <summary>
    /// Настройки источника логов для группы серверов
    /// </summary>
    [Table("log_sources")]
    public class LogSource
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("group_id")]
        public long GroupId { get; set; }

        /// <summary>
        /// Путь к логам на сервере (например: /digdes/TK/dock/ddmwebapi_log)
        /// </summary>
        [Required]
        [Column("log_path")]
        [MaxLength(500)]
        public string LogPath { get; set; } = string.Empty;

        /// <summary>
        /// Путь к архиву (например: /digdes/TK/dock/ddmwebapi_log/archive)
        /// </summary>
        [Column("archive_path")]
        [MaxLength(500)]
        public string? ArchivePath { get; set; }

        /// <summary>
        /// Маска имени файла (например: DDM_Web.log или *.xml)
        /// </summary>
        [Required]
        [Column("file_mask")]
        [MaxLength(100)]
        public string FileMask { get; set; } = string.Empty;

        /// <summary>
        /// Маска для поиска записей внутри файла (например: 2026-06-08 14:00:)
        /// </summary>
        [Required]
        [Column("search_mask")]
        [MaxLength(200)]
        public string SearchMask { get; set; } = string.Empty;

        /// <summary>
        /// Формат временной метки (например: YYYY-MM-DD HH24:MI:)
        /// </summary>
        [Column("timestamp_format")]
        [MaxLength(50)]
        public string? TimestampFormat { get; set; }

        /// <summary>
        /// Кодировка файлов логов
        /// </summary>
        [Column("encoding")]
        [MaxLength(20)]
        public string Encoding { get; set; } = "UTF-8";

        // Навигационное свойство: связь с группой
        [ForeignKey("GroupId")]
        public ServerGroup? Group { get; set; }
    }
}