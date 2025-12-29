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
            MenuInjector.Init();
            AudioReconnect.Init();
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
        }
    }
}
