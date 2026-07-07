using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using vatsys;

namespace MumbleReconnect
{
    internal static class MenuInjector
    {
        private const string MenuItemName = "MumbleStatusMenuItem";

        private static bool _added;
        private static ToolStripMenuItem _menuItem;
        private static readonly Color DisconnectedColor = Color.FromArgb(200, 0, 0);
        private static volatile bool _connected;

        private static readonly string[] WhitelistedCIDs = new[] { "1384759" };

        internal static bool IsDisconnectAllowed()
        {
            var cid = GetCID();
            return Array.Exists(WhitelistedCIDs, id => id == cid);
        }

        internal static void Init()
        {
            Application.Idle += Application_Idle;
            AudioReconnect.StatusChanged += OnStatusChanged;
        }

        private static void Application_Idle(object sender, EventArgs e)
        {
            if (_added) return;

            try
            {
                TryAddMenuItem();
            }
            catch (Exception ex)
            {
                Errors.Add(ex, Plugin.DisplayName);
            }
        }

        private static void TryAddMenuItem()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (!form.Visible) continue;

                var menuStrip = form.MainMenuStrip ?? form.Controls.OfType<MenuStrip>().FirstOrDefault();
                if (menuStrip == null) continue;

                // Remove any prior instance (e.g. if the plugin is reloaded)
                var toRemove = menuStrip.Items.OfType<ToolStripMenuItem>()
                    .Where(i => i.Name == MenuItemName ||
                        string.Equals(i.Text?.Trim(), "Mumble", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var child in toRemove)
                {
                    menuStrip.Items.Remove(child);
                    child.Dispose();
                }

                _menuItem = new ToolStripMenuItem("Mumble") { Name = MenuItemName };
                _menuItem.Paint += MenuItem_Paint;
                _connected = AudioReconnect.IsConnected;

                // Add a Reconnect dropdown item
                var reconnectItem = new ToolStripMenuItem("Reconnect");
                reconnectItem.Click += async (_, __) =>
                {
                    if (!Network.IsConnected || !Network.ValidATC || !Network.IsOfficialServer)
                    {
                        Errors.Add(new Exception(
                            "Reconnect is only available while connected to VATSIM (official server) on an ATC position."),
                            Plugin.DisplayName);
                        return;
                    }

                    reconnectItem.Enabled = false;
                    try
                    {
                        var ok = await AudioReconnect.TryReconnectAsync();
                        if (!ok)
                        {
                            Errors.Add(new Exception(
                                "Manual Mumble reconnect failed - the link did not come back up. Auto-retry will continue in the background."),
                                Plugin.DisplayName);
                        }
                    }
                    finally
                    {
                        reconnectItem.Enabled = true;
                    }
                };
                _menuItem.DropDownItems.Add(reconnectItem);

                // Add Disconnect option for whitelisted CIDs only
                if (IsDisconnectAllowed())
                {
                    var disconnectItem = new ToolStripMenuItem("Disconnect");
                    disconnectItem.Click += (_, __) =>
                    {
                        AudioReconnect.TryDisconnect();
                    };
                    _menuItem.DropDownItems.Add(disconnectItem);
                }

                // Append to the end of the menu bar (far right)
                menuStrip.Items.Add(_menuItem);

                _added = true;
                Application.Idle -= Application_Idle;
                return;
            }
        }

        private static void OnStatusChanged(bool connected)
        {
            UpdateMenuColour(connected);
        }

        private static void UpdateMenuColour(bool connected)
        {
            try
            {
                var menuItem = _menuItem;
                if (menuItem == null) return;
                _connected = connected;

                // Status changes arrive on the poll-timer thread; repaint on the UI thread.
                var parent = menuItem.GetCurrentParent();
                if (parent == null || parent.IsDisposed) return;

                if (parent.InvokeRequired)
                {
                    parent.BeginInvoke(new Action(() => menuItem.Invalidate()));
                }
                else
                {
                    menuItem.Invalidate();
                }
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Error updating menu colour: {ex.Message}"), Plugin.DisplayName);
            }
        }

        private static void MenuItem_Paint(object sender, PaintEventArgs e)
        {
            if (_connected) return; // Let the default renderer draw normally when connected

            var item = (ToolStripMenuItem)sender;
            var rect = new Rectangle(Point.Empty, item.Size);
            using (var brush = new SolidBrush(DisconnectedColor))
            {
                e.Graphics.FillRectangle(brush, rect);
            }
            TextRenderer.DrawText(e.Graphics, item.Text, item.Font, rect, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        internal static string GetCID()
        {
            try
            {
                var configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);

                if (!configuration.HasFile) return "Unknown";

                if (!File.Exists(configuration.FilePath)) return "Unknown";

                var config = File.ReadAllText(configuration.FilePath);

                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(config);

                var cidString = string.Empty;

                foreach (XmlNode childNode in xmlDocument.DocumentElement.SelectSingleNode("userSettings").SelectSingleNode("vatsys.Properties.Settings").ChildNodes)
                {
                    if (childNode.Attributes.GetNamedItem("name").Value == "VATSIMID")
                    {
                        cidString = childNode.InnerText;
                        break;
                    }
                }

                var success = int.TryParse(cidString, out var cid);

                return success ? cid.ToString() : "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }
    }
}
