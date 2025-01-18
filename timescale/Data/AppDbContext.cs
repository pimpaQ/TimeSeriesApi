using Microsoft.EntityFrameworkCore;
using timescale.Models;

namespace timescale.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<Value> Value { get; set; }
        public DbSet<Models.Results> Results { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Value>()
                .HasIndex(v => new { v.Date, v.FileName })
                .IsUnique();
        }
    }
}
