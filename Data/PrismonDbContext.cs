using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Prismon.Api.Models;
//using Prismon.Api.Hubs;

namespace Prismon.Api.Data;

public class PrismonDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Organization> Organizations { get; set; }
    public DbSet<App> Apps { get; set; }
    public DbSet<DAppUser> DAppUsers { get; set; }
    public DbSet<LoginChallenge> LoginChallenges { get; set; }
    public DbSet<AIModel> AIModels { get; set; }
    public DbSet<AIInvocation> AIInvocations { get; set; }
    public DbSet<ApiUsage> ApiUsages { get; set; }
    public DbSet<ApplicationUser> ApplicationUsers { get; set; }
    //public DbSet<TransactionStatsEntity> TransactionStats { get; set; } 

    public PrismonDbContext(DbContextOptions<PrismonDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>()
            .HasKey(u => u.Id); // Id is string from IdentityUser
        builder.Entity<ApplicationUser>()
            .Property(u => u.DeveloperId).IsRequired();
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Organization)
            .WithMany()
            .HasForeignKey(u => u.OrganizationId);

        builder.Entity<Organization>()
            .HasKey(o => o.Id);
        builder.Entity<Organization>()
            .HasOne(o => o.Owner)
            .WithMany()
            .HasForeignKey(o => o.OwnerId); // OwnerId is string, matches ApplicationUser.Id

        builder.Entity<App>()
            .HasKey(a => a.Id);
        builder.Entity<App>()
            .HasOne(a => a.Developer)
            .WithMany(o => o.Apps)
            .HasForeignKey(a => a.DeveloperId); // DeveloperId is string
        builder.Entity<App>()
            .HasOne(a => a.Organization)
            .WithMany(o => o.Apps)
            .HasForeignKey(a => a.OrganizationId);
        builder.Entity<App>()
            .HasIndex(a => a.ApiKey)
            .IsUnique();

        builder.Entity<DAppUser>()
            .HasKey(u => u.Id);
        builder.Entity<DAppUser>()
            .HasOne(u => u.App)
            .WithMany()
            .HasForeignKey(u => u.AppId);

        builder.Entity<LoginChallenge>()
           .HasKey(c => c.Id);

        builder.Entity<LoginChallenge>()
            .HasIndex(c => new { c.AppId, c.WalletPublicKey });


        builder.Entity<AIModel>()
            .HasKey(m => m.Id);
        builder.Entity<AIModel>()
            .HasOne(m => m.App)
            .WithMany()
            .HasForeignKey(m => m.AppId);
        builder.Entity<AIModel>()
       .Property(m => m.ModelName)
       .HasMaxLength(100);

        builder.Entity<AIInvocation>()
            .HasKey(i => i.Id);
        builder.Entity<AIInvocation>()
            .HasOne(i => i.Model)
            .WithMany()
            .HasForeignKey(i => i.ModelId);
        builder.Entity<AIInvocation>()
            .HasIndex(i => i.UserId);
        // Index for performance
        builder.Entity<ApiUsage>()
            .Property(u => u.Timestamp)
            .HasColumnType("timestamp without time zone");

        builder.Entity<ApiUsage>()
            .HasIndex(u => new { u.AppId, u.Timestamp })
            .HasDatabaseName("IX_ApiUsages_AppId_Timestamp");

        builder.Entity<ApiUsage>()
            .HasOne(u => u.App)
            .WithMany()
            .HasForeignKey(u => u.AppId);

    }
}