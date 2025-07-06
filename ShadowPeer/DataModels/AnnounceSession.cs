namespace ShadowPeer.DataModels
{
    internal class AnnounceSession
    {
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public TimeSpan AnnounceInterval { get; set; } = TimeSpan.FromMinutes(30); // Default to 30 minutes
        public TimeSpan MinAnnounceInterval { get; set; } = TimeSpan.FromSeconds(30); // Default to 30 seconds
        public DateTime LastAnnounceTime { get; set; } = DateTime.UtcNow;
        public DateTime LastAnnounceErrorTime { get; set; } = DateTime.UtcNow;



        bool _isSeed => Left == 0;
        public long Left { get; set; } = 0;
        public long TotalDownloaded { get; set; } = 0;
        public long TotalUploaded { get; set; } = 0;
        public int NumWant { get; set; } = 0;



    }
}
