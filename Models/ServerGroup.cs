using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogCollectorApp.Models
{
    /// <summary>
    /// Группа серверов СЭД Правительства Нижегородской области
    /// </summary>
    [Table("server_groups")]
    public class ServerGroup
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационное свойство: у группы может быть много серверов
        public ICollection<Server> Servers { get; set; } = new List<Server>();

        // Навигационное свойство: у группы один источник логов
        public LogSource? LogSource { get; set; }
    }
}