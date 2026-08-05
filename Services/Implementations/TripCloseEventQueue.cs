using System.Threading.Channels;
using ShaloTrack_API.Models;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class TripCloseEventQueue : ITripCloseEventQueue
{
    // Bounded, not unbounded -- a producer bug that enqueues far faster than
    // the consumer can process should apply backpressure or drop, not grow
    // memory without limit. 500 is generously above anything this fleet's
    // current size could produce; DropOldest means a pathological flood loses
    // the oldest queued items first rather than blocking the notification
    // handler thread this was built to keep unblocked.
    private readonly Channel<TripCloseEvent> _channel = Channel.CreateBounded<TripCloseEvent>(
        new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    public void Enqueue(TripCloseEvent tripCloseEvent)
    {
        // TryWrite is non-blocking by design -- the whole point of this queue
        // is that the caller (the NOTIFY handler) never awaits anything slow.
        _channel.Writer.TryWrite(tripCloseEvent);
    }

    public IAsyncEnumerable<TripCloseEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}