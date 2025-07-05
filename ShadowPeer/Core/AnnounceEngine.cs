using ShadowPeer.DataModels;
using System.Diagnostics;

namespace ShadowPeer.Core
{
    internal class AnnounceEngine
    {
        private readonly TorrentMetadatas _torrentMeta;
        private readonly ClientSignature _clientSignature;
        private readonly Stopwatch _trackerIntervallTimer = new();
        private CancellationTokenSource _cancellationTokenSource = new();

        enum AnnounceState { NotStarted, Started, Running, Stopped }



        public AnnounceEngine(TorrentMetadatas torrentMeta, ClientSignature clientSignature)
        {
            ArgumentNullException.ThrowIfNull(torrentMeta, nameof(torrentMeta));
            ArgumentNullException.ThrowIfNull(clientSignature, nameof(clientSignature));

            _clientSignature = clientSignature;
            _torrentMeta = torrentMeta;

        }

        // Wrap all in a single method
        private void StartEngine()
        {


        }


        // Gonna do math and call network methods for the first announce (&event="started")
        private void FirstAnnounce()
        {


        }

        // Gonna do math and call network methods for the regular announce (&event="", should be empty according to BT protocol)
        private void HeartBeatAnnounce()
        {

        }

        // Gonna do math and call network methods for the last announceand stop everything (&event="stopped")
        private void StopEngine()
        {

        }

    }
}
