using ConsoleApp1;
using Microsoft.EntityFrameworkCore;

public class ApplicationDbContext : DbContext
{
    // The DbSet maps your class to a table named "Properties"
    public DbSet<PropertyRecord> Properties { get; set; }

    public DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Use an SQLite connection string. 
        // The file 'appdata.db' will be created in the application directory.
        optionsBuilder.UseSqlite("Data Source=appdata.db");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<User>()
            .HasKey(u => u.Id);

        // Precomputed hash for "admin123"
        string hashedPassword = PasswordHelper.HashPassword("admin123");

        // Seed admin user
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = 1,
            Name = "Admin",
            Email = "admin@arsteg.com",
            Password = hashedPassword
        });

        modelBuilder.Entity<PropertyRecord>(e =>
        {
            e.ToTable("Properties");
            e.Property(p => p.Id).ValueGeneratedOnAdd();

            // Optional helpful indexes:
            e.HasIndex(p => new { p.RadarId, p.Apn }).HasDatabaseName("IX_Properties_Radar_APN");
            e.HasIndex(p => new { p.Address, p.City, p.State }).HasDatabaseName("IX_Properties_Address");
        });

    }
}