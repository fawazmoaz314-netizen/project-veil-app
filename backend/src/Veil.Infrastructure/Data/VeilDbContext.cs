using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Veil.Contracts;
using Veil.Domain.Auth;
using Veil.Domain.Conversations;
using Veil.Domain.Messages;
using Veil.Domain.Users;

namespace Veil.Infrastructure.Data;

public class VeilDbContext(DbContextOptions<VeilDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(user =>
        {
            user.Property(u => u.UserName).HasMaxLength(Limits.UserNameMaxLength);
            user.Property(u => u.NormalizedUserName).HasMaxLength(Limits.UserNameMaxLength);
            user.Property(u => u.Email).HasMaxLength(Limits.EmailMaxLength);
            user.Property(u => u.NormalizedEmail).HasMaxLength(Limits.EmailMaxLength);
            user.Property(u => u.DisplayName).HasMaxLength(Limits.DisplayNameMaxLength).IsRequired();
            user.Property(u => u.CreatedAtUtc).IsRequired();
        });

        builder.Entity<Conversation>(conversation =>
        {
            conversation.ToTable("Conversations");
            conversation.HasKey(c => c.Id);
            conversation.Property(c => c.Type).HasConversion<string>().HasMaxLength(16);
            conversation.Property(c => c.DirectKey).HasMaxLength(65);
            conversation.HasIndex(c => c.DirectKey).IsUnique();
            conversation.HasIndex(c => c.LastActivityAtUtc);
        });

        builder.Entity<ConversationParticipant>(participant =>
        {
            participant.ToTable("ConversationParticipants");
            participant.HasKey(p => new { p.ConversationId, p.UserId });
            participant.HasIndex(p => p.UserId);

            participant.HasOne(p => p.Conversation)
                .WithMany(c => c.Participants)
                .HasForeignKey(p => p.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            participant.HasOne(p => p.User)
                .WithMany(u => u.Participations)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Message>(message =>
        {
            message.ToTable("Messages");
            message.HasKey(m => m.Id);

            message.Property(m => m.Sequence).UseIdentityAlwaysColumn();
            message.HasIndex(m => m.Sequence).IsUnique();
            message.HasIndex(m => new { m.ConversationId, m.Sequence });
            message.HasIndex(m => new { m.SenderId, m.ClientMessageId }).IsUnique();

            message.Property(m => m.Body).HasMaxLength(Limits.MessageBodyMaxLength).IsRequired();
            message.Property(m => m.ContentType).HasMaxLength(Limits.ContentTypeMaxLength).IsRequired();

            message.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            message.HasOne(m => m.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RefreshToken>(token =>
        {
            token.ToTable("RefreshTokens");
            token.HasKey(t => t.Id);
            token.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.HasIndex(t => t.FamilyId);
            token.HasIndex(t => t.UserId);

            token.HasOne(t => t.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
