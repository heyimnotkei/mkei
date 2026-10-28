using System.Drawing;

namespace MKei
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.card = new CardPanel();
            this.lblIn = new System.Windows.Forms.Label();
            this.lblOut = new System.Windows.Forms.Label();
            this.cmbInput = new System.Windows.Forms.ComboBox();
            this.cmbOutput = new System.Windows.Forms.ComboBox();
            this.divider1 = new System.Windows.Forms.Panel();
            this.chkMono = new ToggleSwitch();
            this.statusPill = new StatusPill();
            this.lblVolume = new System.Windows.Forms.Label();
            this.trackVolume = new FlatSlider();
            this.divider2 = new System.Windows.Forms.Panel();
            this.btnStart = new RoundedButton();
            this.btnStop = new RoundedButton();
            this.btnCurrentApps = new RoundedButton();
            this.lblHeader = new System.Windows.Forms.Label();
            this.lblSub = new System.Windows.Forms.Label();

            this.SuspendLayout();

            // Window
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new Size(636, 430);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "";
            this.BackColor = Theme.Window;
            this.Font = Theme.Body;

            // lblHeader / lblSub - quiet page title, sits above the card
            this.lblHeader.AutoSize = true;
            this.lblHeader.Location = new Point(24, 18);
            this.lblHeader.Font = Theme.Title;
            this.lblHeader.ForeColor = Theme.Ink;
            this.lblHeader.Text = "MKei";

            this.lblSub.AutoSize = true;
            this.lblSub.Location = new Point(26, 46);
            this.lblSub.Font = Theme.BodySmall;
            this.lblSub.ForeColor = Theme.InkMuted;
            this.lblSub.Text = "Fold a captured source down to mono and play it back.";

            this.btnSettings = new RoundedButton();
            this.btnSettings.Location = new Point(24, 68);
            this.btnSettings.Size = new Size(112, 30);
            this.btnSettings.Text = "⚙ Settings";
            this.btnSettings.Variant = ButtonVariant.Ghost;
            this.btnSettings.Click += new System.EventHandler(this.BtnSettings_Click);

            // card
            this.card.Location = new Point(20, 106);
            this.card.Size = new Size(596, 304);
            this.card.Radius = 14;

            // lblIn
            this.lblIn.AutoSize = true;
            this.lblIn.Location = new Point(20, 18);
            this.lblIn.Font = Theme.Heading;
            this.lblIn.ForeColor = Theme.InkMuted;
            this.lblIn.Text = "INPUT  ·  RECORDING";

            // lblOut
            this.lblOut.AutoSize = true;
            this.lblOut.Location = new Point(302, 18);
            this.lblOut.Font = Theme.Heading;
            this.lblOut.ForeColor = Theme.InkMuted;
            this.lblOut.Text = "OUTPUT  ·  PLAYBACK";

            // cmbInput
            this.cmbInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbInput.FormattingEnabled = true;
            this.cmbInput.Location = new Point(20, 40);
            this.cmbInput.Size = new Size(258, 28);
            ComboStyler.Style(this.cmbInput);

            // cmbOutput
            this.cmbOutput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbOutput.FormattingEnabled = true;
            this.cmbOutput.Location = new Point(302, 40);
            this.cmbOutput.Size = new Size(258, 28);
            ComboStyler.Style(this.cmbOutput);

            // divider1
            this.divider1.BackColor = Theme.Divider;
            this.divider1.Location = new Point(20, 86);
            this.divider1.Size = new Size(540, 1);

            // chkMono
            this.chkMono.Location = new Point(20, 100);
            this.chkMono.Size = new Size(210, 26);
            this.chkMono.Text = "Force mono output";
            this.chkMono.CheckedChanged += new System.EventHandler(this.chkMono_CheckedChanged);

            // statusPill
            this.statusPill.Location = new Point(302, 100);
            this.statusPill.Size = new Size(258, 26);

            // lblVolume
            this.lblVolume.AutoSize = true;
            this.lblVolume.Location = new Point(20, 148);
            this.lblVolume.Font = Theme.Heading;
            this.lblVolume.ForeColor = Theme.InkMuted;
            this.lblVolume.Text = "VOLUME  ·  100%";

            // trackVolume
            this.trackVolume.Location = new Point(20, 174);
            this.trackVolume.Size = new Size(540, 28);
            this.trackVolume.Minimum = 0;
            this.trackVolume.Maximum = 100;
            this.trackVolume.Value = 100;
            this.trackVolume.Scroll += new System.EventHandler(this.trackVolume_Scroll);

            // divider2
            this.divider2.BackColor = Theme.Divider;
            this.divider2.Location = new Point(20, 218);
            this.divider2.Size = new Size(540, 1);

            // btnStart
            this.btnStart.Location = new Point(20, 240);
            this.btnStart.Size = new Size(140, 40);
            this.btnStart.Text = "Start";
            this.btnStart.Variant = ButtonVariant.Solid;
            this.btnStart.AccentColor = Theme.Sage;
            this.btnStart.AccentColorDark = Theme.SageDark;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);

            // btnStop
            this.btnStop.Location = new Point(172, 240);
            this.btnStop.Size = new Size(140, 40);
            this.btnStop.Text = "Stop";
            this.btnStop.Variant = ButtonVariant.Outline;
            this.btnStop.AccentColor = Theme.Clay;
            this.btnStop.AccentColorDark = Theme.ClayDark;
            this.btnStop.Click += new System.EventHandler(this.btnStop_Click);

            // btnCurrentApps
            this.btnCurrentApps.Location = new Point(400, 240);
            this.btnCurrentApps.Size = new Size(160, 40);
            this.btnCurrentApps.Text = "Current Apps";
            this.btnCurrentApps.Variant = ButtonVariant.Ghost;
            this.btnCurrentApps.Click += new System.EventHandler(this.BtnCurrentApps_Click);

            // card contents
            this.card.Controls.Add(this.lblIn);
            this.card.Controls.Add(this.lblOut);
            this.card.Controls.Add(this.cmbInput);
            this.card.Controls.Add(this.cmbOutput);
            this.card.Controls.Add(this.divider1);
            this.card.Controls.Add(this.chkMono);
            this.card.Controls.Add(this.statusPill);
            this.card.Controls.Add(this.lblVolume);
            this.card.Controls.Add(this.trackVolume);
            this.card.Controls.Add(this.divider2);
            this.card.Controls.Add(this.btnStart);
            this.card.Controls.Add(this.btnStop);
            this.card.Controls.Add(this.btnCurrentApps);

            // form contents
            this.Controls.Add(this.lblHeader);
            this.Controls.Add(this.lblSub);
            this.Controls.Add(this.btnSettings);
            this.Controls.Add(this.card);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private CardPanel card;
        private System.Windows.Forms.Label lblHeader;
        private System.Windows.Forms.Label lblSub;
        private RoundedButton btnStart;
        private RoundedButton btnStop;
        private ToggleSwitch chkMono;
        private System.Windows.Forms.ComboBox cmbInput;
        private System.Windows.Forms.ComboBox cmbOutput;
        private System.Windows.Forms.Label lblIn;
        private System.Windows.Forms.Label lblOut;
        private StatusPill statusPill;
        private FlatSlider trackVolume;
        private System.Windows.Forms.Label lblVolume;
        private System.Windows.Forms.Panel divider1;
        private System.Windows.Forms.Panel divider2;
        private RoundedButton btnCurrentApps;
        private RoundedButton btnSettings;
    }
}
