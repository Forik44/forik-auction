using ForikAuction.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ForikAuction.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomMember> RoomMembers => Set<RoomMember>();
    public DbSet<UserTalent> UserTalents => Set<UserTalent>();
    public DbSet<Auction> Auctions => Set<Auction>();
    public DbSet<AuctionEntry> AuctionEntries => Set<AuctionEntry>();
    public DbSet<RoomQuest> RoomQuests => Set<RoomQuest>();
    public DbSet<QuestApprovalVote> QuestApprovalVotes => Set<QuestApprovalVote>();
    public DbSet<PointsLedgerEntry> PointsLedger => Set<PointsLedgerEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(u => u.GoogleSubject).IsUnique();
        b.Entity<Room>().HasIndex(r => r.JoinCode).IsUnique();

        b.Entity<RoomMember>()
            .HasIndex(m => new { m.RoomId, m.UserId }).IsUnique();
        b.Entity<RoomMember>()
            .HasOne(m => m.Room).WithMany(r => r.Members)
            .HasForeignKey(m => m.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<RoomMember>()
            .HasOne(m => m.User).WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<UserTalent>()
            .HasIndex(t => new { t.RoomMemberId, t.Code }).IsUnique();

        b.Entity<QuestApprovalVote>()
            .HasIndex(v => new { v.RoomQuestId, v.VoterRoomMemberId }).IsUnique();
        b.Entity<QuestApprovalVote>()
            .HasOne(v => v.RoomQuest).WithMany(q => q.Votes)
            .HasForeignKey(v => v.RoomQuestId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<AuctionEntry>()
            .HasOne(e => e.Auction).WithMany(a => a.Entries)
            .HasForeignKey(e => e.AuctionId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AuctionEntry>()
            .HasOne(e => e.RoomMember).WithMany(m => m.Entries)
            .HasForeignKey(e => e.RoomMemberId).OnDelete(DeleteBehavior.NoAction);
    }
}
