using GRP_03_27.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GRP_03_27.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Administration Subsystem
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Fridge> Fridges { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Location> Locations { get; set; }

        // Customer Management Subsystem
        public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
        public DbSet<AllocationHistory> AllocationHistories { get; set; }
        public DbSet<NewFridgeRequest> NewFridgeRequests { get; set; }

        // Fridge Fault Subsystem
        public DbSet<FaultReport> FaultReports { get; set; }

        // Fridge Maintenance Subsystem
        public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<ServiceHistoryEntry> ServiceHistoryEntries { get; set; }

        // Notifications
        public DbSet<Notification> Notifications { get; set; }

        // Password History
        public DbSet<PasswordHistory> PasswordHistories { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Fix decimal precision issues
            builder.Entity<ServiceHistoryEntry>()
                .Property(s => s.Cost)
                .HasPrecision(18, 2);

            builder.Entity<PurchaseRequest>()
                .Property(p => p.EstimatedCost)
                .HasPrecision(18, 2);

            builder.Entity<MaintenanceRecord>()
                .Property(m => m.TotalCost)
                .HasPrecision(18, 2);

            // Configure relationships and constraints
            builder.Entity<Fridge>()
                .HasOne(f => f.Customer)
                .WithMany(c => c.Fridges)
                .HasForeignKey(f => f.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            // Fault Report relationships
            builder.Entity<FaultReport>()
                .HasOne(fr => fr.Fridge)
                .WithMany(f => f.FaultReports)
                .HasForeignKey(fr => fr.FridgeId)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);

            builder.Entity<FaultReport>()
                .HasOne(fr => fr.Customer)
                .WithMany(c => c.FaultReports)
                .HasForeignKey(fr => fr.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // MAINTENANCE RECORD RELATIONSHIPS - ADD THESE
            builder.Entity<MaintenanceRecord>()
                .HasOne(mr => mr.Fridge)
                .WithMany(f => f.MaintenanceRecords)
                .HasForeignKey(mr => mr.FridgeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<MaintenanceRecord>()
                .HasOne(mr => mr.AssignedTechnician)
                .WithMany(u => u.AssignedMaintenance)
                .HasForeignKey(mr => mr.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.Restrict);

            // SERVICE HISTORY RELATIONSHIPS - ADD THESE
            builder.Entity<ServiceHistoryEntry>()
                .HasOne(sh => sh.Fridge)
                .WithMany(f => f.ServiceHistory)
                .HasForeignKey(sh => sh.FridgeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ServiceHistoryEntry>()
                .HasOne(sh => sh.Technician)
                .WithMany(u => u.ServiceHistory)
                .HasForeignKey(sh => sh.TechnicianId)
                .OnDelete(DeleteBehavior.Restrict);

            // FAULT REPORT TECHNICIAN RELATIONSHIP - ADD THIS IF MISSING
            builder.Entity<FaultReport>()
                .HasOne(fr => fr.AssignedTechnician)
                .WithMany(u => u.AssignedFaultReports)
                .HasForeignKey(fr => fr.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.Restrict);

            // Location relationship
            builder.Entity<Location>()
                .HasOne(l => l.Customer)
                .WithMany() // Assuming one location per customer
                .HasForeignKey(l => l.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Notification relationship
            builder.Entity<Notification>()
                .HasOne(n => n.Customer)
                .WithMany() // Assuming no navigation back to notifications
                .HasForeignKey(n => n.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Password History relationship
            builder.Entity<PasswordHistory>()
                .HasOne(ph => ph.User)
                .WithMany()
                .HasForeignKey(ph => ph.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Customer-User relationship (make it optional initially)
            builder.Entity<Customer>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.SetNull); // Use SetNull instead of Cascade

            // Allocation History relationships
            builder.Entity<AllocationHistory>()
                .HasOne(ah => ah.Fridge)
                .WithMany(f => f.AllocationHistories)
                .HasForeignKey(ah => ah.FridgeId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<AllocationHistory>()
                .HasOne(ah => ah.Customer)
                .WithMany()
                .HasForeignKey(ah => ah.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<AllocationHistory>()
                .HasOne(ah => ah.ActionBy)
                .WithMany()
                .HasForeignKey(ah => ah.ActionById)
                .OnDelete(DeleteBehavior.Restrict);

            // NewFridgeRequest relationships
            builder.Entity<NewFridgeRequest>()
                .HasOne(nfr => nfr.Customer)
                .WithMany(c => c.NewFridgeRequests)
                .HasForeignKey(nfr => nfr.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Index for better query performance
            builder.Entity<Fridge>()
                .HasIndex(f => f.Status);

            builder.Entity<FaultReport>()
                .HasIndex(fr => fr.Status);

            builder.Entity<MaintenanceRecord>()
                .HasIndex(mr => mr.ScheduledDate);

            builder.Entity<MaintenanceRecord>()
                .HasIndex(mr => mr.Status);

            builder.Entity<ServiceHistoryEntry>()
                .HasIndex(sh => sh.ServiceDate);

            builder.Entity<PasswordHistory>()
                .HasIndex(ph => ph.UserId);

            builder.Entity<Customer>()
                .HasIndex(c => c.UserId);

            builder.Entity<AllocationHistory>()
                .HasIndex(ah => ah.ActionDate);

            builder.Entity<AllocationHistory>()
                .HasIndex(ah => ah.FridgeId);

            // Configure enums as strings for better readability in database
            builder.Entity<Fridge>()
                .Property(f => f.Status)
                .HasConversion<string>();

            builder.Entity<FaultReport>()
                .Property(fr => fr.Status)
                .HasConversion<string>();

            builder.Entity<FaultReport>()
                .Property(fr => fr.Priority)
                .HasConversion<string>();

            builder.Entity<MaintenanceRecord>()
                .Property(mr => mr.MaintenanceType)
                .HasConversion<string>();

            builder.Entity<MaintenanceRecord>()
                .Property(mr => mr.Status)
                .HasConversion<string>();

            builder.Entity<MaintenanceRecord>()
                .Property(mr => mr.Priority)
                .HasConversion<string>();

            builder.Entity<ServiceHistoryEntry>()
                .Property(sh => sh.ServiceType)
                .HasConversion<string>();

            builder.Entity<PurchaseRequest>()
                .Property(pr => pr.Status)
                .HasConversion<string>();

            builder.Entity<PurchaseRequest>()
                .Property(pr => pr.Priority)
                .HasConversion<string>();

            builder.Entity<Customer>()
                .Property(c => c.Type)
                .HasConversion<string>();
        }
    }
}