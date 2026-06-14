using System.ComponentModel.DataAnnotations;
namespace ShaloTrack_API.Models
{
    public class RawPacket
    {
        [Key]
        public long PacketId { get; set; }
        public Guid DeviceId { get; set; }
        public string ProtocolNumber { get; set; } = string.Empty;
        public string RawHex { get; set; } = string.Empty;
        public int PacketLength { get; set; }
        public DateTime ReceivedAt { get; set; }
        public bool Parsed { get; set; }
        public GpsDevice Device { get; set; } = null!;
    }
}
