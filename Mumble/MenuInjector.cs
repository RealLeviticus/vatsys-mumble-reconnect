using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using vatsys;

namespace MumbleReconnect
{
    internal static class MenuInjector
    {
        private static bool _added;
        private static ToolStripMenuItem _menuItem;
        private static readonly Color DisconnectedColor = Color.FromArgb(200, 0, 0);
        private static bool _connected;

        private static readonly string[] WhitelistedCIDs = new[] { "1384759" };

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
                _ = DiscordLogger.LogMumbleError("Error adding Mumble menu item", ex);
            }
        }

        private static void TryAddMenuItem()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (!form.Visible) continue;

                var menuStrip = form.MainMenuStrip ?? form.Controls.OfType<MenuStrip>().FirstOrDefault();
                if (menuStrip == null) continue;

                // Remove any prior top-level instance
                var toRemove = menuStrip.Items.OfType<ToolStripMenuItem>()
                    .Where(i => string.Equals(i.Text.Trim(), "Mumble Status", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var child in toRemove)
                {
                    menuStrip.Items.Remove(child);
                }

                _menuItem = new ToolStripMenuItem("Mumble") { Name = "MumbleStatusMenuItem" };
                _menuItem.Paint += MenuItem_Paint;
                _connected = AudioReconnect.IsConnected;

                // Add a Reconnect dropdown item
                var reconnectItem = new ToolStripMenuItem("Reconnect");
                reconnectItem.Click += async (_, __) =>
                {
                    if (!Network.IsConnected || !Network.ValidATC || !Network.IsOfficialServer)
                    {
                        MessageBox.Show(
                            "Reconnect is only available while connected to VATSIM (official server) on an ATC position.",
                            Plugin.DisplayName,
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }

                    reconnectItem.Enabled = false;
                    var ok = await AudioReconnect.TryReconnectAsync();
                    if (!ok)
                    {
                        MessageBox.Show("Reconnect failed. Check logs for details.",
                            Plugin.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    reconnectItem.Enabled = true;
                };
                _menuItem.DropDownItems.Add(reconnectItem);

                // Add Disconnect option for whitelisted CIDs only
                var cid = DiscordLogger.GetCID();
                if (Array.Exists(WhitelistedCIDs, id => id == cid))
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
                if (_menuItem == null) return;
                _connected = connected;
                _menuItem.Invalidate();
            }
            catch (Exception ex)
            {
                Errors.Add(new Exception($"Error updating menu colour: {ex.Message}"), Plugin.DisplayName);
                _ = DiscordLogger.LogMumbleError("Error updating Mumble menu colour", ex);
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
    }
}
