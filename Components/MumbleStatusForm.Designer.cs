using System;
using System.Drawing;
using System.Windows.Forms;
using vatsys;

namespace MumbleReconnect
{
    partial class MumbleStatusForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private vatsys.GenericButton btnReconnect;
        private vatsys.GenericButton btnDisconnect;
        private vatsys.TextLabel lblStatusLabel;
        private vatsys.TextLabel lblStatusValue;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.btnReconnect = new vatsys.GenericButton();
            this.btnDisconnect = new vatsys.GenericButton();
            this.lblStatusLabel = new vatsys.TextLabel();
            this.lblStatusValue = new vatsys.TextLabel();
            this.SuspendLayout();
            //
            // btnReconnect
            //
            this.btnReconnect.Font = new System.Drawing.Font("Terminus (TTF)", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.btnReconnect.Location = new System.Drawing.Point(12, 92);
            this.btnReconnect.Name = "btnReconnect";
            this.btnReconnect.Size = new System.Drawing.Size(150, 35);
            this.btnReconnect.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnReconnect.SubText = "";
            this.btnReconnect.TabIndex = 1;
            this.btnReconnect.Text = "Reconnect";
            this.btnReconnect.UseVisualStyleBackColor = true;
            this.btnReconnect.Click += new System.EventHandler(this.BtnReconnect_Click);
            //
            // btnDisconnect
            //
            this.btnDisconnect.Font = new System.Drawing.Font("Terminus (TTF)", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.btnDisconnect.Location = new System.Drawing.Point(180, 92);
            this.btnDisconnect.Name = "btnDisconnect";
            this.btnDisconnect.Size = new System.Drawing.Size(150, 35);
            this.btnDisconnect.SubFont = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnDisconnect.SubText = "";
            this.btnDisconnect.TabIndex = 2;
            this.btnDisconnect.Text = "Disconnect";
            this.btnDisconnect.UseVisualStyleBackColor = true;
            this.btnDisconnect.Click += new System.EventHandler(this.BtnDisconnect_Click);
            //
            // lblStatusLabel
            //
            this.lblStatusLabel.AutoSize = true;
            this.lblStatusLabel.Font = new System.Drawing.Font("Terminus (TTF)", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.lblStatusLabel.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.lblStatusLabel.HasBorder = false;
            this.lblStatusLabel.InteractiveText = false;
            this.lblStatusLabel.Location = new System.Drawing.Point(12, 20);
            this.lblStatusLabel.Name = "lblStatusLabel";
            this.lblStatusLabel.Size = new System.Drawing.Size(120, 17);
            this.lblStatusLabel.TabIndex = 0;
            this.lblStatusLabel.Text = "Mumble Status:";
            //
            // lblStatusValue
            //
            this.lblStatusValue.AutoSize = true;
            this.lblStatusValue.Font = new System.Drawing.Font("Terminus (TTF)", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            this.lblStatusValue.ForeColor = System.Drawing.SystemColors.ControlDark;
            this.lblStatusValue.HasBorder = false;
            this.lblStatusValue.InteractiveText = false;
            this.lblStatusValue.Location = new System.Drawing.Point(12, 50);
            this.lblStatusValue.Name = "lblStatusValue";
            this.lblStatusValue.Size = new System.Drawing.Size(87, 16);
            this.lblStatusValue.TabIndex = 1;
            this.lblStatusValue.Text = "Unknown...";
            //
            // MumbleStatusForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(342, 139);
            this.Controls.Add(this.lblStatusValue);
            this.Controls.Add(this.lblStatusLabel);
            this.Controls.Add(this.btnDisconnect);
            this.Controls.Add(this.btnReconnect);
            this.HasMinimizeButton = false;
            this.HasSendBackButton = false;
            this.MiddleClickClose = false;
            this.Name = "MumbleStatusForm";
            this.Resizeable = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Mumble Status";
            this.TopMost = true;
            this.Shown += new System.EventHandler(this.MumbleStatusForm_Shown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
    }
}
