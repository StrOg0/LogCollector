using LogCollectorApp.Configuration;
using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogCollectorApp.DAl
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

            modelBuilder.HasDefaultSchema("log_collector");

            modelBuilder.Entity<ServerGroup>().ToTable("server_groups", schema: "log_collector");
            modelBuilder.Entity<Server>().ToTable("servers", schema: "log_collector");
            modelBuilder.Entity<LogSource>().ToTable("log_sources", schema: "log_collector");

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

            modelBuilder.Entity<Server>()
                .Property(s => s.SshPort)
                .HasDefaultValue(22);

            // Преобразование ip-адреса в строку
            modelBuilder.Entity<Server>()
                .Property(s => s.IpAddress)
                .HasConversion(
                    v => System.Net.IPAddress.Parse(v), // Как сохранить string из C# в inet в БД
                    v => v.ToString()                   // Как прочитать inet из БД в string в C#
                )
                .HasColumnType("inet");

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