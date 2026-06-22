using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogCollectorApp.Models
{
    [Table("servers", Schema = "log_collector")]
    public class Server
    {
        [Key]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("group_id")]
        public long GroupId { get; set; }

        [Required]
        [Column("name")]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("ip_address")]
        [MaxLength(50)]
        public string IpAddress { get; set; } = string.Empty;

        [Column("ssh_port")]
        public int SshPort { get; set; } = 22;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("GroupId")]
        public ServerGroup? Group { get; set; }

        /// <summary>
        /// Флаг выбора сервера для сбора логов (не сохраняется в БД)
        /// </summary>
        [NotMapped]
        public bool IsSelected { get; set; }
    }
}