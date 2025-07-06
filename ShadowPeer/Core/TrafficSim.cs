using System;

namespace ShadowPeer.Core
{


    /// <summary>
    /// Simulates network traffic for uploading data with a random speed between specified limits.
    /// </summary>

    internal class TrafficSim
    {
        private readonly Random _rnd = new();
        private DateTime _lastTick = DateTime.UtcNow;

        private readonly long _targetBytes;
        private long _uploadedBytes;
        private readonly long _minSpeed;
        private readonly long _maxSpeed;

        public long CurrentUploadSpeed { get; private set; } = 0;
        public long TotalUploadedBytes => _uploadedBytes;
        public bool isDone => _uploadedBytes >= _targetBytes;

        public TrafficSim(long targetBytesToUpload, long minSpeed, long maxSpeed)
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
            var delta = now - _lastTick;
            _lastTick = now;

            double seconds = delta.TotalSeconds;

            if (seconds <= 0)
                return;

            // Random speed for each tick
            long speed = _rnd.NextInt64(_minSpeed, _maxSpeed + 1);
            long bytesToUpload = (long)(speed * seconds);

            if (_uploadedBytes + bytesToUpload > _targetBytes)
                bytesToUpload = _targetBytes - _uploadedBytes;

            _uploadedBytes += bytesToUpload;
            CurrentUploadSpeed = speed;
        }
    }
}
