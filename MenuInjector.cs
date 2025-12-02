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
        private static readonly Color ConnectedColor = Color.FromArgb(30, 150, 40);
        private static readonly Color DisconnectedColor = Color.FromArgb(180, 60, 60);
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
            }
        }

        private static void TryAddMenuItem()
        {
            foreach (Form form in Application.OpenForms)
            {
                if (!form.Visible) continue;

                var menuStrip = form.MainMenuStrip ?? form.Controls.OfType<MenuStrip>().FirstOrDefault();
                if (menuStrip == null) continue;

                // Remove any prior dropdown instance.
                foreach (var top in menuStrip.Items.OfType<ToolStripMenuItem>())
                {
                    var toRemove = top.DropDownItems.OfType<ToolStripMenuItem>()
                        .Where(i => string.Equals(i.Text.Trim(), "Mumble Status", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    foreach (var child in toRemove)
                    {
                        top.DropDownItems.Remove(child);
                    }
                }

                // Already at top level?
                if (menuStrip.Items.OfType<ToolStripMenuItem>()
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
            catch { }
        }

        private static void MenuItem_DrawItem(object sender, PaintEventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            Color back = _connected ? ConnectedColor : DisconnectedColor;
            using (var backBrush = new SolidBrush(back))
            using (var foreBrush = new SolidBrush(Color.White))
            {
                e.Graphics.FillRectangle(backBrush, item.Bounds);
                TextRenderer.DrawText(e.Graphics, item.Text, item.Font, item.Bounds, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
