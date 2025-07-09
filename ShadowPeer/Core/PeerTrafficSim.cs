namespace ShadowPeer.Core
{

    /// <summary>
    /// Emulates peer-to-peer network traffic patterns, dynamically computing upload throughput 
    /// over time with randomized bandwidth fluctuations within defined limits.
    /// </summary>


    // TODO : Simulate download traffic


    internal class PeerTrafficSim
    {
        private readonly Random _rnd = new();
        private DateTime _lastTick = DateTime.UtcNow;


        private long _TorrentSizeBytes;

        private readonly long _targetBytes;
        private long _uploadedBytes;
        private readonly long _minSpeed;
        private readonly long _maxSpeed;
        private bool _hasCompleted = false;

        // Get speed in bytes per second
        public long CurrentUploadSpeed { get; private set; } = 0;
        // Get total uploaded bytes
        public long TotalUploadedBytes => _uploadedBytes;

        // Called when the upload simulation reach the target bytes
        public event EventHandler? UploadCompleted;

        public PeerTrafficSim(long targetBytesToUpload, long minSpeed, long maxSpeed)
        {
            if (minSpeed <= 0 || maxSpeed <= 0 || minSpeed > maxSpeed)
                throw new ArgumentException("Speed values must be positive and minSpeed <= maxSpeed.");

            if (targetBytesToUpload <= 0)
                throw new ArgumentException("Target upload bytes must be greater than zero.");

            _targetBytes = targetBytesToUpload;
            _minSpeed = minSpeed;
            _maxSpeed = maxSpeed;
        }

        public void Tick()
        {
            var now = DateTime.UtcNow;
            if (now <= _lastTick)
                return;

            var seconds = (now - _lastTick).TotalSeconds;
            _lastTick = now;

            if (seconds <= 0 || _hasCompleted)
                return;

            var speed = _rnd.NextInt64(_minSpeed, _maxSpeed + 1);
            var bytesToUpload = (long)(speed * seconds);
            var bytesRemaining = _targetBytes - _uploadedBytes;

            if (bytesToUpload >= bytesRemaining)
            {
                _uploadedBytes = _targetBytes;
                CurrentUploadSpeed = speed;
                _hasCompleted = true;
                UploadCompleted?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _uploadedBytes += bytesToUpload;
                CurrentUploadSpeed = speed;
            }
        }


        public void Reset()
        {
            _uploadedBytes = 0;
            _lastTick = DateTime.UtcNow;
            CurrentUploadSpeed = 0;
            _hasCompleted = false;
        }
    }

}
