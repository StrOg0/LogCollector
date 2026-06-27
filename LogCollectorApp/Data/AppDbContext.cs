using LogCollectorApp.Configuration;
using LogCollectorApp.Helpers;
using LogCollectorApp.Models;
using Microsoft.EntityFrameworkCore;

namespace LogCollectorApp.Data;

public class AppDbContext : DbContext
{
    public DbSet<ServerGroup> ServerGroups { get; set; } = null!;
    public DbSet<Server> Servers { get; set; } = null!;
    public DbSet<LogSource> LogSources { get; set; } = null!;

    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured) optionsBuilder.UseNpgsql(DatabaseConfig.ConnectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("pm02");

        modelBuilder.Entity<ServerGroup>().ToTable("server_groups", "pm02");
        modelBuilder.Entity<Server>().ToTable("servers", "pm02");
        modelBuilder.Entity<LogSource>().ToTable("log_sources", "pm02");

        ConfigureCreatedAt(modelBuilder.Entity<Server>().Property(s => s.CreatedAt));
        ConfigureCreatedAt(modelBuilder.Entity<ServerGroup>().Property(g => g.CreatedAt));

        modelBuilder.Entity<ServerGroup>()
            .HasMany(g => g.Servers)
            .WithOne(s => s.Group)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServerGroup>()
            .HasOne(g => g.LogSource)
            .WithOne(s => s.Group)
            .HasForeignKey<LogSource>(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Server>()
            .Property(s => s.IpAddress)
            .HasColumnName("ip_address")
            .HasColumnType("inet")
            .HasConversion(
                value => IpAddressDbConverter.ToDatabase(value),
                value => IpAddressDbConverter.FromDatabase(value));

        modelBuilder.Entity<Server>().Property(s => s.SshPort).HasDefaultValue(22);
        modelBuilder.Entity<Server>().Property(s => s.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<ServerGroup>().Property(g => g.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<LogSource>().Property(s => s.Encoding).HasDefaultValue("UTF-8");
    }

    private static void ConfigureCreatedAt(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<DateTime> property)
    {
        property
            .HasConversion(v => v.ToUniversalTime(), v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
            .ValueGeneratedOnAdd();
    }
}
