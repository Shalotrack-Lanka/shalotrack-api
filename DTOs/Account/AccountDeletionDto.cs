namespace ShaloTrack_API.DTOs.Account;

public class DeleteAccountRequestDto
{
    /// <summary>Must be the word DELETE (see AccountDeletion.ConfirmWord).</summary>
    public string? Confirm { get; set; }
}

public class DeletionStatusDto
{
    public bool Pending { get; set; }
    public DateTime? RequestedAt { get; set; }

    /// <summary>When the data will be permanently erased (request + 30 days).</summary>
    public DateTime? ScheduledFor { get; set; }
    public int DaysLeft { get; set; }
    public int GraceDays { get; set; }
}