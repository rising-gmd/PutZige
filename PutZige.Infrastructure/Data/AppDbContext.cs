#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PutZige.Domain.Entities;
using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace PutZige.Infrastructure.Data;

/// <summary>
/// Application database context that applies enterprise conventions such as
/// soft-delete, automatic timestamping, and configuration discovery.
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Creates a new instance of <see cref="AppDbContext"/>.
    /// </summary>
    /// <param name="options">DbContext options.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    /// <summary>
    /// Users table set.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Messages table set.
    /// </summary>
    public DbSet<Message> Messages => Set<Message>();

    /// <summary>
    /// Message attachments table set.
    /// </summary>
    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    /// <summary>
    /// Message stars table set.
    /// </summary>
    public DbSet<MessageStar> MessageStars => Set<MessageStar>();

    /// <summary>
    /// Conversations table set.
    /// </summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>
    /// Conversation participants table set.
    /// </summary>
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    /// <summary>
    /// User settings table set.
    /// </summary>
    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    /// <summary>
    /// User sessions table set.
    /// </summary>
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    /// <summary>
    /// User rate limits table set.
    /// </summary>
    public DbSet<UserRateLimit> UserRateLimits => Set<UserRateLimit>();

    /// <summary>
    /// User metadata table set.
    /// </summary>
    public DbSet<UserMetadata> UserMetadata => Set<UserMetadata>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration implementations in this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Global query filter for soft deletes on BaseEntity-derived types
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                // Only apply filter when property exists and is of type bool
                var isDeletedProp = entityType.FindProperty(nameof(BaseEntity.IsDeleted));
                if (isDeletedProp != null && isDeletedProp.ClrType == typeof(bool))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(GenerateSoftDeleteFilter(entityType.ClrType));
                }
            }
        }
    }

    private static LambdaExpression GenerateSoftDeleteFilter(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "e");
        var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
        var condition = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(condition, parameter);
    }

    /// <summary>
    /// Override that sets CreatedAt/UpdatedAt/DeletedAt and converts deletes into soft-deletes.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;

        // Iterate entries for BaseEntity types only
        var entries = ChangeTracker.Entries<BaseEntity>();

        foreach (var entry in entries)
        {
            if (entry.Entity is null)
                continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.Id == Guid.Empty)
                        entry.Entity.Id = Guid.NewGuid();

                    entry.Entity.CreatedAt = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = utcNow;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = utcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
