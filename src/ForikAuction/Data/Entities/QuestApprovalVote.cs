namespace ForikAuction.Data.Entities;

/// <summary>Голос участника комнаты за то, что игрок действительно выполнил квест.</summary>
public class QuestApprovalVote
{
    public int Id { get; set; }
    public int RoomQuestId { get; set; }
    public RoomQuest RoomQuest { get; set; } = null!;
    public int VoterRoomMemberId { get; set; }
    public bool Approve { get; set; }
}
