using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using vatsys;

namespace MumbleReconnect
{
    internal partial class MumbleStatusForm : BaseForm
    {
        private readonly Color _statusConnected = Color.FromArgb(0, 128, 0);
        private readonly Color _statusDisconnected = Color.FromArgb(200, 0, 0);

        private readonly Action<bool> _statusChangedHandler;

        private static MumbleStatusForm _instance;

        internal static void ShowWindow()
        {
            if (_instance == null || _instance.IsDisposed)
            {
                _instance = new MumbleStatusForm();
            }

            _instance.UpdateStatus(AudioReconnect.IsConnected);
            _instance.Show();
            _instance.BringToFront();
            _instance.Activate();
        }

        public MumbleStatusForm()
        {
            InitializeComponent();
            StyleComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || DesignMode)
            {
                return;
            }

            AudioReconnect.StatusChanged += _statusChangedHandler = connected => SafeUpdateStatus(connected);

            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                    return;
                }

                AudioReconnect.StatusChanged -= _statusChangedHandler;
                _instance = null;
            };
        }

        private void MumbleStatusForm_Shown(object sender, EventArgs e)
        {
            UpdateStatus(AudioReconnect.IsConnected);
        }

        private void SafeUpdateStatus(bool connected)
        {
            if (IsDisposed) return;

            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => UpdateStatus(connected)));
            }
            else
            {
                UpdateStatus(connected);
            }
        }

        private void UpdateStatus(bool connected)
        {
            if (connected)
            {
                lblStatusValue.Text = "Connected";
                lblStatusValue.ForeColor = _statusConnected;
            }
            else
            {
                // Show different message if not connected to VATSIM network
                lblStatusValue.Text = !Network.IsConnected ? "Not connected to VATSIM" : "Disconnected";
                lblStatusValue.ForeColor = _statusDisconnected;
            }

            // Always allow manual control.
            btnReconnect.Enabled = true;
            btnDisconnect.Enabled = true;
        }

        private void StyleComponent()
        {
            // Style labels - using vatSys theme colors
            lblStatusLabel.BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            lblStatusLabel.ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);

            lblStatusValue.BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            lblStatusValue.ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);

            // Style buttons - using vatSys GenericButton defaults
            btnReconnect.BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            btnReconnect.ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);

            btnDisconnect.BackColor = Colours.GetColour(Colours.Identities.WindowBackground);
            btnDisconnect.ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            btnDisconnect.Enabled = false;
            AudioReconnect.TryDisconnect();
            btnDisconnect.Enabled = true;
        }

        private async void BtnReconnect_Click(object sender, EventArgs e)
        {
            btnReconnect.Enabled = false;

            // Require an active ATC connection/position on the real network to avoid abuse.
            if (!Network.IsConnected || !Network.ValidATC || !Network.IsOfficialServer)
            {
                MessageBox.Show(this,
                    "Reconnect is only available while connected to VATSIM (official server) on an ATC position.",
                    Plugin.DisplayName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                btnReconnect.Enabled = true;
                return;
            }

            var ok = await AudioReconnect.TryReconnectAsync();
            if (!ok)
            {
                MessageBox.Show(this, "Reconnect failed. Check logs for details.", Plugin.DisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            btnReconnect.Enabled = true;
        }
    }
}
