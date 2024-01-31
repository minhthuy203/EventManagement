using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Principal;

namespace EventManagement.Models;

public class EventDbContext : IdentityDbContext<ApplicationUser>
{

    public EventDbContext(DbContextOptions<EventDbContext> options)
        : base(options)
    {
    }
    public DbSet<Event> Events { get; set; }
    public DbSet<EventUser> EventUsers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .HasMany(user => user.RegisteredEvents)
            .WithOne(eventEntity => eventEntity.User)
            .HasForeignKey(eventEntity => eventEntity.UserId);

        modelBuilder.Entity<EventUser>()
            .HasKey(eu => new { eu.UserId, eu.EventId });

        modelBuilder.Entity<EventUser>()
            .HasOne(eu => eu.User)
            .WithMany(user => user.RegisteredEvents)
            .HasForeignKey(eu => eu.UserId);

        modelBuilder.Entity<EventUser>()
            .HasOne(eu => eu.Event)
            .WithMany(eventEntity => eventEntity.Participants)
            .HasForeignKey(eu => eu.EventId);
    }

}