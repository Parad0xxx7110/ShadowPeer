using ShadowPeer.DataModels;
using System.Diagnostics;

namespace ShadowPeer.Core
{
    internal class AnnounceEngine : IDisposable
    {
        private readonly TorrentMetadatas _torrentMeta;
        private readonly ClientSignature _clientSignature;
        private readonly Stopwatch _trackerIntervallTimer = new();
        private CancellationTokenSource _cts = new();

       



        enum AnnounceEngineState { Ready, Starting, Running, Stopped, Error }



        public AnnounceEngine(TorrentMetadatas metas, ClientSignature signature)
        {
            ArgumentNullException.ThrowIfNull(metas, nameof(metas));
            ArgumentNullException.ThrowIfNull(signature, nameof(signature));

            _clientSignature = signature;
            _torrentMeta = metas;

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

        public void Dispose()
        {
            ((IDisposable)_cts).Dispose();
        }
    }
}
