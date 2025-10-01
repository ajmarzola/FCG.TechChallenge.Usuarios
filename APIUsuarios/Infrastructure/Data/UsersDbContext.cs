using APIUsuarios.Domain;
using Microsoft.EntityFrameworkCore;

namespace APIUsuarios.Infrastructure.Data
{
    public class UsersDbContext : DbContext
    {
        public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }
        public DbSet<User> Users => Set<User>();


        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<User>(e =>
            {
                e.ToTable("Users");
                e.HasKey(x => x.Id);
                e.Property(x => x.Email).IsRequired().HasMaxLength(160);
                e.Property(x => x.Nome).IsRequired().HasMaxLength(160);
                e.Property(x => x.PasswordHash).IsRequired();
                e.Property(x => x.Role).IsRequired();
                e.HasIndex(x => x.Email).IsUnique();
            });
        }
    }
}
