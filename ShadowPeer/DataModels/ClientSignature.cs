namespace ShadowPeer.DataModels
{
    public class ClientSignature
    {
        public string UserAgent { get; set; } = null!;
        public string Version { get; set; } = null!;
        public string PeerId { get; set; } = null!;
        public string Key { get; set; } = null!;
    }
}
