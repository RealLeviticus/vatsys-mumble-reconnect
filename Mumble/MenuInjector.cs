using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using vatsys;

namespace MumbleReconnect
{
    internal static class MenuInjector
    {
        private static bool _added;
        private static ToolStripMenuItem _menuItem;
        private static readonly Color ConnectedColor = Color.FromArgb(0, 150, 0);
        private static readonly Color DisconnectedColor = Color.FromArgb(200, 0, 0);
        private static bool _connected;

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
                DiscordLogger.LogMumbleError("Error adding Mumble menu item", ex);
            }
        }

        private static void TryAddMenuItem()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (!form.Visible) continue;

                var menuStrip = form.MainMenuStrip ?? form.Controls.OfType<MenuStrip>().FirstOrDefault();
                if (menuStrip == null) continue;

                // Find the Settings menu
                var settingsMenu = menuStrip.Items.OfType<ToolStripMenuItem>()
                    .FirstOrDefault(i => string.Equals(i.Text.Trim(), "Settings", StringComparison.OrdinalIgnoreCase));

                if (settingsMenu == null) continue;

                // Remove any prior instance from Settings dropdown
                var toRemove = settingsMenu.DropDownItems.OfType<ToolStripMenuItem>()
                    .Where(i => string.Equals(i.Text.Trim(), "Mumble Status", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var child in toRemove)
                {
                    settingsMenu.DropDownItems.Remove(child);
                }

                // Already exists in Settings?
                if (settingsMenu.DropDownItems.OfType<ToolStripMenuItem>()
                    .Any(i => string.Equals(i.Text.Trim(), "Mumble Status", StringComparison.OrdinalIgnoreCase)))
                {
                    _added = true;
                    Application.Idle -= Application_Idle;
                    return;
                }

                _menuItem = new ToolStripMenuItem("Mumble Status") { Name = "MumbleStatusMenuItem" };
                _menuItem.Click += (_, __) => MumbleStatusForm.ShowWindow();
                _menuItem.Paint += MenuItem_DrawItem;
                _connected = AudioReconnect.IsConnected;
                UpdateMenuColour(_connected);

                // Add to Settings dropdown
                settingsMenu.DropDownItems.Add(_menuItem);

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
                DiscordLogger.LogMumbleError("Error updating Mumble menu colour", ex);
            }
        }

        private static void MenuItem_DrawItem(object sender, PaintEventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            Color back = _connected ? ConnectedColor : DisconnectedColor;
            using (var backBrush = new SolidBrush(back))
            {
                e.Graphics.FillRectangle(backBrush, item.Bounds);
                TextRenderer.DrawText(e.Graphics, item.Text, item.Font, item.Bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
