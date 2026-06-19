using LogCollectorApp.Configuration;
using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogCollectorApp.Data
{
    /// <summary>
    /// Контекст базы данных для работы с PostgreSQL
    /// Связывает модели C# с таблицами в схеме pm02 БД logs_collecting
    /// </summary>
    public class AppDbContext : DbContext
    {
        /// <summary>
        /// Таблица групп серверов (pm02.server_groups)
        /// </summary>
        public DbSet<ServerGroup> ServerGroups { get; set; }

        /// <summary>
        /// Таблица серверов (pm02.servers)
        /// </summary>
        public DbSet<Server> Servers { get; set; }

        /// <summary>
        /// Таблица источников логов (pm02.log_sources)
        /// </summary>
        public DbSet<LogSource> LogSources { get; set; }

        /// <summary>
        /// Конструктор по умолчанию
        /// </summary>
        public AppDbContext()
        {
        }

        /// <summary>
        /// Конструктор с возможностью передачи настроек
        /// </summary>
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        /// <summary>
        /// Настройка подключения к БД
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Если опции не были переданы извне, используем строку подключения из конфигурации
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(DatabaseConfig.ConnectionString);
            }
        }

        /// <summary>
        /// Дополнительная настройка моделей (маппинг, связи, схема)
        /// </summary>
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.HasDefaultSchema("pm02");

    modelBuilder.Entity<ServerGroup>().ToTable("server_groups", schema: "pm02");
    modelBuilder.Entity<Server>().ToTable("servers", schema: "pm02");
    modelBuilder.Entity<LogSource>().ToTable("log_sources", schema: "pm02");

    modelBuilder.Entity<ServerGroup>()
        .HasMany(sg => sg.Servers)
        .WithOne(s => s.Group)
        .HasForeignKey(s => s.GroupId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<ServerGroup>()
        .HasOne(sg => sg.LogSource)
        .WithOne(ls => ls.Group)
        .HasForeignKey<LogSource>(ls => ls.GroupId)
        .OnDelete(DeleteBehavior.Cascade);

    // 🔥 УБРАН конвертер для IP-адреса - теперь это просто string

    modelBuilder.Entity<Server>()
        .Property(s => s.SshPort)
        .HasDefaultValue(22);

    modelBuilder.Entity<ServerGroup>()
        .Property(sg => sg.IsActive)
        .HasDefaultValue(true);

    modelBuilder.Entity<Server>()
        .Property(s => s.IsActive)
        .HasDefaultValue(true);

    modelBuilder.Entity<LogSource>()
        .Property(ls => ls.Encoding)
        .HasDefaultValue("UTF-8");
}
    }
}