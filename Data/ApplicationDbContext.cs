using Microsoft.EntityFrameworkCore;
using ScanerServer.Models;

namespace ScanerServer.Data
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<HttpRequest> HttpRequests { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=scaner_server.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<HttpRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Method).IsRequired().HasMaxLength(10);
                entity.Property(e => e.Path).IsRequired().HasMaxLength(500);
                entity.Property(e => e.Headers).HasMaxLength(4000);
                entity.Property(e => e.Body).HasMaxLength(10000);
                entity.Property(e => e.ClientIp).HasMaxLength(45);
                entity.Property(e => e.Timestamp).IsRequired();
                entity.Property(e => e.IsCopied).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.Type).HasMaxLength(100);
            });
        }
    }
}
