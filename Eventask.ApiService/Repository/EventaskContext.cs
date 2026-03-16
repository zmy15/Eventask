using Eventask.Domain.Entity.Calendars;
using Eventask.Domain.Entity.Calendars.ScheduleItems;
using Eventask.Domain.Entity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Eventask.ApiService.Repository;

public class EventaskContext : DbContext
{
    public EventaskContext(DbContextOptions<EventaskContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Calendar> Calendars { get; set; }
    public DbSet<ScheduleItem> ScheduleItems { get; set; }
    public DbSet<SpecialDay> SpecialDays { get; set; }
    //public DbSet<Attachment> Attachments { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        var utcConverter = new ValueConverter<DateTimeOffset, DateTime>(
            v => v.UtcDateTime,
            v => new DateTimeOffset(v));

        builder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserName).IsRequired();
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.DeletedAt).HasConversion(utcConverter);
        });

        builder.Entity<Calendar>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.DeletedAt).HasConversion(utcConverter);
            entity.HasOne<User>()
                .WithMany(u => u.Calendars)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany<ScheduleItem>()
                .WithOne(u => u.Calendar)
                .HasForeignKey(u => u.CalendarId);

            //entity.OwnsMany(e => e.ScheduleItems,
            //    item =>
            //    {
            //        item.HasKey(e => e.Id);
            //        item.Property(e => e.Title).IsRequired();
            //        item.Property(e => e.Version).IsConcurrencyToken();
            //        item.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
            //        item.Property(e => e.IsDeleted).IsRequired();
            //        item.Property(e => e.DeletedAt).HasConversion(utcConverter);
            //        item.HasOne(e => e.Calendar)
            //            .WithMany(c => c.ScheduleItems)
            //            .HasForeignKey(e => e.CalendarId);
            //    });
        });

        builder.Entity<CalendarMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CalendarId, e.UserId }).IsUnique();
            entity.Property(e => e.Role).HasConversion<string>().IsRequired();
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.DeletedAt).HasConversion(utcConverter);
            entity.HasOne(e => e.Calendar)
                .WithMany(c => c.Members)
                .HasForeignKey(e => e.CalendarId);
            entity.HasOne<User>()
                .WithMany(u => u.CalendarMemberships)
                .HasForeignKey(e => e.UserId);
        });

        builder.Entity<SpecialDay>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).HasColumnType("date").IsRequired();
            entity.Property(e => e.Type).HasConversion<string>().IsRequired();
            entity.HasIndex(e => e.Date).IsUnique();
        });

        builder.Entity<ScheduleItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.Property(e => e.DeletedAt).HasConversion(utcConverter);
            entity.HasOne(e => e.Calendar)
                .WithMany(c => c.ScheduleItems)
                .HasForeignKey(e => e.CalendarId);
            entity.OwnsMany(e => e.Attachments, b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.FileName).IsRequired();
                b.Property(e => e.ContentType).IsRequired();
                b.Property(e => e.ObjectKey).IsRequired();
                b.Property(e => e.Version).IsConcurrencyToken();
                b.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
                b.Property(e => e.IsDeleted).IsRequired();
                b.Property(e => e.DeletedAt).HasConversion(utcConverter);
                b.WithOwner(x => x.ScheduleItem).HasForeignKey(x => x.ScheduleItemId);
            });

            entity.HasDiscriminator<string>("ScheduleItemType")
                .HasValue<ScheduleEvent>("Event")
                .HasValue<ScheduleTask>("Task");
        });

        builder.Entity<ScheduleEvent>(entity =>
        {
            entity.Property(e => e.StartAt).HasConversion(utcConverter).IsRequired();
            entity.Property(e => e.EndAt).HasConversion(utcConverter).IsRequired();
            //entity.HasOne(e => e.RecurrenceRule)
            //    .WithOne(r => r.ScheduleItem)
            //    .HasForeignKey<RecurrenceRule>(r => r.ScheduleItemId);

            entity.OwnsOne(e => e.RecurrenceRule, b =>
            {
                b.ToTable("RecurrenceRules");
                b.HasKey(e => e.Id);
                b.Property(e => e.Freq).HasConversion<string>().IsRequired();
                b.Property(e => e.Interval).HasDefaultValue(1);
                b.Property(e => e.Version).IsConcurrencyToken();
                b.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
                b.Property(e => e.DeletedAt).HasConversion(utcConverter);
                b.Property(e => e.Until).HasConversion(utcConverter);
                b.Property(e => e.IsDeleted).IsRequired();
                b.WithOwner(r => r.ScheduleItem);
            });
        });

        builder.Entity<ScheduleTask>(entity =>
        {
            entity.Property(e => e.IsCompleted).IsRequired();
            entity.Property(e => e.DueAt).HasConversion(utcConverter).IsRequired();
            entity.Property(e => e.CompletedAt).HasConversion(utcConverter).IsRequired();
        });

        //builder.Entity<RecurrenceRule>(entity =>
        //{
        //    entity.HasKey(e => e.Id);
        //    entity.Property(e => e.Freq).HasConversion<string>().IsRequired();
        //    entity.Property(e => e.Interval).HasDefaultValue(1);
        //    entity.Property(e => e.Version).IsConcurrencyToken();
        //    entity.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
        //    entity.Property(e => e.DeletedAt).HasConversion(utcConverter);
        //    entity.Property(e => e.Until).HasConversion(utcConverter);
        //    entity.Property(e => e.IsDeleted).IsRequired();
        //});

        builder.Entity<Reminder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Version).IsConcurrencyToken();
            entity.Property(e => e.UpdatedAt).HasConversion(utcConverter).IsRequired();
            entity.Property(e => e.DeletedAt).HasConversion(utcConverter);
            entity.Property(e => e.IsDeleted).IsRequired();
            entity.HasOne(e => e.ScheduleItem)
                .WithMany(s => s.Reminders)
                .HasForeignKey(e => e.ScheduleItemId);
        });
    }
}