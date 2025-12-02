using System;
using System.ComponentModel.Composition;
using vatsys;
using vatsys.Plugin;

namespace MumbleReconnect
{
    [Export(typeof(IPlugin))]
    public class Plugin : IPlugin
    {
        public string Name => "Mumble Reconnect";
        public static string DisplayName => "Mumble Reconnect";

        public Plugin()
        {
            Audio.VSCSFrequenciesChanged += Audio_VSCSFrequenciesChanged;
            Audio.FrequencyErrorStateChanged += Audio_VSCSFrequenciesChanged;
            Network.PrimaryFrequencyChanged += Audio_VSCSFrequenciesChanged;
            Network.Connected += Network_Connected;
            Network.Disconnected += Network_Disconnected;

            MenuInjector.Init();
            AudioReconnect.Init();
        }

        private void Audio_VSCSFrequenciesChanged(object sender, EventArgs e)
        {
            // No-op: present to mirror original plugin hooks; extend later if needed.
        }

        private void Network_Disconnected(object sender, EventArgs e)
        {
            // Nothing to do beyond AudioReconnect handling.
        }

        private void Network_Connected(object sender, EventArgs e)
        {
            // Nothing to do beyond AudioReconnect handling.
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
            // Not used in this plugin.
        }

        public async void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
            await System.Threading.Tasks.Task.CompletedTask;
        }
    }
}
