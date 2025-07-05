namespace ShadowPeer.DataModels
{
    internal class TrackerResponse
    {
        public int? Seeders { get; set; }         
        public int? Leechers { get; set; }       
        public int? Interval { get; set; }
        public int? MinInterval { get; set; }
        public byte[]? PeersCompact { get; set; }      // compact format
        public List<Peer>? PeersList { get; set; }  // list format
        public bool IsCompact => PeersCompact != null; // Flag to check if the response is in compact format / old dictionary format
    }
}
