namespace ShaloTrack_API.DTOs.Command;

public class CommandHistoryItemDto
{
    public long Id { get; set; }
    public string Command { get; set; } = string.Empty;
    public string? RawResponse { get; set; }
    public object? ParsedData { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CommandHistoryResponseDto
{
    public string Imei { get; set; } = string.Empty;
    public List<CommandHistoryItemDto> History { get; set; } = new();
    public int Count { get; set; }
}