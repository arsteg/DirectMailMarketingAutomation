using MailMerge.Data.Helpers;
using MailMerge.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace MailMerge.Data
{
    public class MailMergeDbContext : DbContext
    {
        public MailMergeDbContext(DbContextOptions<MailMergeDbContext> options) : base(options)
        {
        }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    // Replace with your actual SQL Server connection string
        //    optionsBuilder.UseSqlServer(
        //        "Server=localhost;Database=MailMergeDb;Trusted_Connection=True;TrustServerCertificate=True;");
        //}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<FollowUpStage>()
                 .HasKey(u => u.Id);

            modelBuilder.Entity<PrinterSettings>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<LeadSource>()
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

        public DbSet<User> Users { get; set; }

        public DbSet<PropertyRecord> Properties { get; set; }

        public DbSet<Campaign> Campaigns { get; set; }
    }
}
