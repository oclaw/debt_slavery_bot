using Microsoft.EntityFrameworkCore;

namespace DebtSlaveryBot.Model
{
    public class DebtDbContext : DbContext
    {
        public DebtDbContext(DbContextOptions<DebtDbContext> options) : base(options) {}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().HasIndex(h => h.Name).IsUnique();
            modelBuilder.Entity<DebtEvent>().HasIndex(e => e.Name).IsUnique();

            // TODO: check if it is unique index
            // modelBuilder.Entity<TotalDebt>().HasIndex(t => new { t.From, t.To }).IsUnique();

            modelBuilder.Entity<User>()
                .HasMany(u => u.DebtEvents)
                .WithMany(e => e.Users)
                .UsingEntity(j => j.ToTable("debt_event_user"));
        }

        public DbSet<User> Users { get; set; }
        public DbSet<TgDetails> TgUsers { get; set; }
        public DbSet<DebtEvent> Events { get; set; }
        public DbSet<TotalDebt> TotalDebts { get; set; }
        public DbSet<Debt> Debts { get; set; }
    }
}
