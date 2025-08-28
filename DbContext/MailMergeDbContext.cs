using Microsoft.EntityFrameworkCore;

namespace MailMerge.Data
{
    public class MailMergeDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Replace with your actual SQL Server connection string
            optionsBuilder.UseSqlServer(
                "Server=localhost;Database=MailMergeDb;Trusted_Connection=True;TrustServerCertificate=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Lead>()
                .HasIndex(l => new { l.Address1, l.FirstName })
                .IsUnique();
        }

        public DbSet<Lead> Leads { get; set; }
    }

    public class Lead
    {
        public int Id { get; set; }  // Primary key

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string BarcodeData { get; set; }
    }
}
