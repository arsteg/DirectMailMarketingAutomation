using MailMerge.Data.Helpers;
using MailMerge.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata;

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

            modelBuilder.Entity<Template>()
                .HasKey(u => u.Id);

            modelBuilder.Entity<Campaign>(campaign =>
            {
                campaign.HasKey(c => c.Id);
                campaign.Property(c => c.Name).HasMaxLength(255).IsRequired();
                campaign.Property(c => c.OutputPath).HasMaxLength(255);
                // 2. Configure 'LeadSource' as a single Owned Entity (Maps to columns in the Campaign table)
                // The properties of LeadSource (SourceType, Priority) will appear directly in the Campaign table
                campaign.OwnsOne(c => c.LeadSource, leadSource =>
                {
                    leadSource.ToTable("Campaigns"); // Still maps to the Campaign table
                });

                // 3. Configure 'PrinterSettings' instances as distinct Owned Entities

                // LetterPrinter
                campaign.OwnsOne(c => c.LetterPrinter, printer =>
                {
                 
                    printer.ToTable("Campaigns");
                });

                // EnvelopePrinter
                campaign.OwnsOne(c => c.EnvelopePrinter, printer =>
                {
                    
                    printer.ToTable("Campaigns");
                });

                // 4. Configure 'Stages' as an Owned Entity Collection (Maps to a separate table)
                campaign.OwnsMany(c => c.Stages, stage =>
                {
                    // The owned entity (FollowUpStage) must have a key, which EF Core manages internally.
                    // It will automatically create a shadow property for the foreign key back to Campaign.

                    // Set the name of the new table
                    stage.ToTable("CampaignFollowUpStages");

                    // Customize the primary key column names in the new table
                    stage.HasKey(x=>x.Id); // EF Core will add a shadow property 'Id' by convention

                    // Add a unique identifier for the specific Campaign
                    stage.WithOwner().HasForeignKey("CampaignId");

                    // Customize the property names in the new table
                    stage.Property(s => s.StageName).HasMaxLength(100);
                });
            });




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

        public DbSet<Template> Templates { get; set; }

        public DbSet<PropertyRecord> Properties { get; set; }

        public DbSet<Campaign> Campaigns { get; set; }
    }
}
