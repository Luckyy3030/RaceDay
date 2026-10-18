using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using Route = RaceDay.Api.Models.Route;

namespace RaceDay.Api.Data;

public partial class RaceDayContext : DbContext
{
    public RaceDayContext(DbContextOptions<RaceDayContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Enrolment> Enrolments { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<EventCategory> EventCategories { get; set; }

    public virtual DbSet<Organiser> Organisers { get; set; }

    public virtual DbSet<Participant> Participants { get; set; }

    public virtual DbSet<Result> Results { get; set; }

    public virtual DbSet<Route> Routes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Categori__19093A0B18B374B3");

            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.DistanceKm).HasColumnType("decimal(7, 2)");
        });

        modelBuilder.Entity<Enrolment>(entity =>
        {
            entity.HasKey(e => e.EnrolmentId).HasName("PK__Enrolmen__5C0E5FCFFF4AF899");

            entity.Property(e => e.EnrolmentDate).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Status).HasMaxLength(30);

            entity.HasOne(d => d.EventCategory).WithMany(p => p.Enrolments)
                .HasForeignKey(d => d.EventCategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Enrolments_EventCategories");

            entity.HasOne(d => d.Participant).WithMany(p => p.Enrolments)
                .HasForeignKey(d => d.ParticipantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Enrolments_Participants");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("PK__Events__7944C8106DCDF93B");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.EventName).HasMaxLength(200);
            entity.Property(e => e.EventType).HasMaxLength(20);
            entity.Property(e => e.Location).HasMaxLength(200);

            entity.HasOne(d => d.Organiser).WithMany(p => p.Events)
                .HasForeignKey(d => d.OrganiserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Events_Organisers");
        });

        modelBuilder.Entity<EventCategory>(entity =>
        {
            entity.HasKey(e => e.EventCategoryId).HasName("PK__EventCat__7174DEBE301AD119");

            entity.HasIndex(e => new { e.EventId, e.CategoryId }, "UQ_EventCategories_Event_Category").IsUnique();

            entity.Property(e => e.EntryFee).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Category).WithMany(p => p.EventCategories)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EventCategories_Categories");

            entity.HasOne(d => d.Event).WithMany(p => p.EventCategories)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_EventCategories_Events");
        });

        modelBuilder.Entity<Organiser>(entity =>
        {
            entity.HasKey(e => e.OrganiserId).HasName("PK__Organise__9C452B6DA328B52A");

            entity.Property(e => e.OrganiserId).ValueGeneratedNever();
            entity.Property(e => e.OrganisationName).HasMaxLength(200);

            entity.HasOne(d => d.OrganiserNavigation).WithOne(p => p.Organiser)
                .HasForeignKey<Organiser>(d => d.OrganiserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Organisers_Users");
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(e => e.ParticipantId).HasName("PK__Particip__7227995E0DF5CF1C");

            entity.Property(e => e.ParticipantId).ValueGeneratedNever();
            entity.Property(e => e.EmergencyContact).HasMaxLength(100);

            entity.HasOne(d => d.ParticipantNavigation).WithOne(p => p.Participant)
                .HasForeignKey<Participant>(d => d.ParticipantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Participants_Users");
        });

        modelBuilder.Entity<Result>(entity =>
        {
            entity.HasKey(e => e.ResultId).HasName("PK__Results__976902082893FEE4");

            entity.HasIndex(e => e.EnrolmentId, "UQ__Results__5C0E5FCE9D4FEBC1").IsUnique();

            entity.Property(e => e.FinishTime).HasPrecision(0);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(sysdatetime())");

            entity.HasOne(d => d.Enrolment).WithOne(p => p.Result)
                .HasForeignKey<Result>(d => d.EnrolmentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Results_Enrolments");
        });

        modelBuilder.Entity<Route>(entity =>
        {
            entity.HasKey(e => e.RouteId).HasName("PK__Routes__80979B4DF66F8196");

            entity.Property(e => e.DistanceKm).HasColumnType("decimal(7, 2)");
            entity.Property(e => e.RouteDescription).HasMaxLength(1000);
            entity.Property(e => e.RouteName).HasMaxLength(200);

            entity.HasOne(d => d.Event).WithMany(p => p.Routes)
                .HasForeignKey(d => d.EventId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Routes_Events");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C5F930F63");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D10534B248D64A").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysdatetime())");
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Role).HasMaxLength(20);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
