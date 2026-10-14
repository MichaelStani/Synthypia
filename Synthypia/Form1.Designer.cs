namespace Synthypia
{
    partial class Form1
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.uiTimer = new System.Windows.Forms.Timer(this.components);
            this.panelTop = new System.Windows.Forms.Panel();
            this.seek = new Synthypia.MetroSlider();
            this.lblTime = new System.Windows.Forms.Label();
            this.tempo = new Synthypia.MetroSlider();
            this.lblTempo = new System.Windows.Forms.Label();
            this.btnStop = new Synthypia.MetroButton();
            this.btnPlay = new Synthypia.MetroButton();
            this.btnOpen = new Synthypia.MetroButton();
            this.panelSide = new System.Windows.Forms.FlowLayoutPanel();
            this.lblSecMode = new System.Windows.Forms.Label();
            this.cbMode = new Synthypia.MetroComboBox();
            this.cbHands = new Synthypia.MetroComboBox();
            this.lblSecSound = new System.Windows.Forms.Label();
            this.cbSound = new Synthypia.MetroComboBox();
            this.lblSecOut = new System.Windows.Forms.Label();
            this.cbOut = new Synthypia.MetroComboBox();
            this.vstRow = new System.Windows.Forms.FlowLayoutPanel();
            this.btnVst = new Synthypia.MetroButton();
            this.btnVstEditor = new Synthypia.MetroButton();
            this.lblSecIn = new System.Windows.Forms.Label();
            this.cbIn = new Synthypia.MetroComboBox();
            this.lblSecVolume = new System.Windows.Forms.Label();
            this.volume = new Synthypia.MetroSlider();
            this.lblSecFall = new System.Windows.Forms.Label();
            this.fall = new Synthypia.MetroSlider();
            this.lblSecOptions = new System.Windows.Forms.Label();
            this.chkMetro = new Synthypia.MetroCheckBox();
            this.chkNames = new Synthypia.MetroCheckBox();
            this.chkSheet = new Synthypia.MetroCheckBox();
            this.lblSecKeys = new System.Windows.Forms.Label();
            this.cbKeys = new Synthypia.MetroComboBox();
            this.lblSecLight = new System.Windows.Forms.Label();
            this.cbLight = new Synthypia.MetroComboBox();
            this.lblLightStatus = new System.Windows.Forms.Label();
            this.panelOct = new System.Windows.Forms.Panel();
            this.lblOct = new System.Windows.Forms.Label();
            this.btnOctDown = new Synthypia.MetroButton();
            this.btnOctUp = new Synthypia.MetroButton();
            this.chkPcKeys = new Synthypia.MetroCheckBox();
            this.lblSecLoop = new System.Windows.Forms.Label();
            this.loopRow = new System.Windows.Forms.FlowLayoutPanel();
            this.btnLoopA = new Synthypia.MetroButton();
            this.btnLoopB = new Synthypia.MetroButton();
            this.btnLoopClear = new Synthypia.MetroButton();
            this.chkLoop = new Synthypia.MetroCheckBox();
            this.lblLoop = new System.Windows.Forms.Label();
            this.lblSecTracks = new System.Windows.Forms.Label();
            this.trackPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.sheet = new Synthypia.SheetView();
            this.view = new Synthypia.PianoView();
            this.panelTop.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.seek)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.tempo)).BeginInit();
            this.panelSide.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.volume)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.fall)).BeginInit();
            this.loopRow.SuspendLayout();
            this.SuspendLayout();
            //
            // uiTimer
            //
            this.uiTimer.Interval = 16;
            this.uiTimer.Tick += new System.EventHandler(this.UiTimer_Tick);
            //
            // panelTop
            //
            this.panelTop.BackColor = System.Drawing.Color.FromArgb(32, 36, 50);
            this.panelTop.Controls.Add(this.seek);
            this.panelTop.Controls.Add(this.lblTime);
            this.panelTop.Controls.Add(this.tempo);
            this.panelTop.Controls.Add(this.lblTempo);
            this.panelTop.Controls.Add(this.btnStop);
            this.panelTop.Controls.Add(this.btnPlay);
            this.panelTop.Controls.Add(this.btnOpen);
            this.panelTop.Dock = System.Windows.Forms.DockStyle.Top;
            this.panelTop.Name = "panelTop";
            this.panelTop.Padding = new System.Windows.Forms.Padding(8);
            this.panelTop.Size = new System.Drawing.Size(1280, 52);
            this.panelTop.TabIndex = 2;
            //
            // seek
            //
            this.seek.Dock = System.Windows.Forms.DockStyle.Fill;
            this.seek.Maximum = 1000;
            this.seek.Name = "seek";
            this.seek.TabIndex = 0;
            this.seek.TabStop = false;
            this.seek.TickStyle = System.Windows.Forms.TickStyle.None;
            this.seek.Scroll += new System.EventHandler(this.Seek_Scroll);
            this.seek.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Slider_MouseUp);
            //
            // lblTime
            //
            this.lblTime.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblTime.Name = "lblTime";
            this.lblTime.Size = new System.Drawing.Size(110, 36);
            this.lblTime.Text = "0:00 / 0:00";
            this.lblTime.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // tempo
            //
            this.tempo.Dock = System.Windows.Forms.DockStyle.Right;
            this.tempo.Maximum = 200;
            this.tempo.Minimum = 25;
            this.tempo.Name = "tempo";
            this.tempo.Size = new System.Drawing.Size(130, 36);
            this.tempo.TabStop = false;
            this.tempo.TickStyle = System.Windows.Forms.TickStyle.None;
            this.tempo.Value = 100;
            this.tempo.Scroll += new System.EventHandler(this.Tempo_Scroll);
            this.tempo.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Slider_MouseUp);
            //
            // lblTempo
            //
            this.lblTempo.Dock = System.Windows.Forms.DockStyle.Right;
            this.lblTempo.Name = "lblTempo";
            this.lblTempo.Size = new System.Drawing.Size(190, 36);
            this.lblTempo.Text = "Tempo 100 %";
            this.lblTempo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // btnStop
            //
            this.btnStop.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnStop.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnStop.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnStop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStop.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(80, 36);
            this.btnStop.TabStop = false;
            this.btnStop.Text = "■ Stopp";
            this.btnStop.UseVisualStyleBackColor = false;
            this.btnStop.Click += new System.EventHandler(this.BtnStop_Click);
            //
            // btnPlay
            //
            this.btnPlay.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnPlay.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnPlay.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnPlay.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPlay.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(90, 36);
            this.btnPlay.TabStop = false;
            this.btnPlay.Text = "▶ Start";
            this.btnPlay.UseVisualStyleBackColor = false;
            this.btnPlay.Click += new System.EventHandler(this.BtnPlay_Click);
            //
            // btnOpen
            //
            this.btnOpen.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnOpen.Dock = System.Windows.Forms.DockStyle.Left;
            this.btnOpen.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnOpen.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOpen.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(90, 36);
            this.btnOpen.TabStop = false;
            this.btnOpen.Text = "Öffnen …";
            this.btnOpen.UseVisualStyleBackColor = false;
            this.btnOpen.Click += new System.EventHandler(this.BtnOpen_Click);
            //
            // panelSide
            //
            this.panelSide.AutoScroll = true;
            this.panelSide.BackColor = System.Drawing.Color.FromArgb(32, 36, 50);
            this.panelSide.Controls.Add(this.lblSecMode);
            this.panelSide.Controls.Add(this.cbMode);
            this.panelSide.Controls.Add(this.cbHands);
            this.panelSide.Controls.Add(this.lblSecSound);
            this.panelSide.Controls.Add(this.cbSound);
            this.panelSide.Controls.Add(this.lblSecOut);
            this.panelSide.Controls.Add(this.cbOut);
            this.panelSide.Controls.Add(this.vstRow);
            this.panelSide.Controls.Add(this.lblSecIn);
            this.panelSide.Controls.Add(this.cbIn);
            this.panelSide.Controls.Add(this.lblSecVolume);
            this.panelSide.Controls.Add(this.volume);
            this.panelSide.Controls.Add(this.lblSecFall);
            this.panelSide.Controls.Add(this.fall);
            this.panelSide.Controls.Add(this.lblSecKeys);
            this.panelSide.Controls.Add(this.cbKeys);
            this.panelSide.Controls.Add(this.lblSecLight);
            this.panelSide.Controls.Add(this.cbLight);
            this.panelSide.Controls.Add(this.lblLightStatus);
            this.panelSide.Controls.Add(this.lblSecOptions);
            this.panelSide.Controls.Add(this.chkMetro);
            this.panelSide.Controls.Add(this.chkNames);
            this.panelSide.Controls.Add(this.chkSheet);
            this.panelSide.Controls.Add(this.chkPcKeys);
            this.panelSide.Controls.Add(this.lblSecLoop);
            this.panelSide.Controls.Add(this.loopRow);
            this.panelSide.Controls.Add(this.chkLoop);
            this.panelSide.Controls.Add(this.lblLoop);
            this.panelSide.Controls.Add(this.lblSecTracks);
            this.panelSide.Controls.Add(this.trackPanel);
            this.panelSide.Dock = System.Windows.Forms.DockStyle.Right;
            this.panelSide.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.panelSide.Name = "panelSide";
            this.panelSide.Padding = new System.Windows.Forms.Padding(8);
            this.panelSide.Size = new System.Drawing.Size(290, 728);
            this.panelSide.TabIndex = 1;
            this.panelSide.WrapContents = false;
            //
            // lblSecMode
            //
            this.lblSecMode.AutoSize = true;
            this.lblSecMode.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecMode.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecMode.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecMode.Name = "lblSecMode";
            this.lblSecMode.Text = "ÜBUNGSMODUS";
            //
            // cbMode
            //
            this.cbMode.BackColor = System.Drawing.Color.FromArgb(44, 49, 68);
            this.cbMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbMode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbMode.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.cbMode.Items.AddRange(new object[] {
            "Zuhören",
            "Warten auf mich",
            "Spielen mit Wertung"});
            this.cbMode.Name = "cbMode";
            this.cbMode.Size = new System.Drawing.Size(270, 28);
            this.cbMode.TabStop = false;
            this.cbMode.SelectedIndexChanged += new System.EventHandler(this.CbMode_SelectedIndexChanged);
            this.cbMode.DropDownClosed += new System.EventHandler(this.Combo_DropDownClosed);
            //
            // cbHands
            //
            this.cbHands.BackColor = System.Drawing.Color.FromArgb(44, 49, 68);
            this.cbHands.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbHands.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbHands.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.cbHands.Items.AddRange(new object[] {
            "Beide Hände üben",
            "Nur rechte Hand üben",
            "Nur linke Hand üben"});
            this.cbHands.Name = "cbHands";
            this.cbHands.Size = new System.Drawing.Size(270, 28);
            this.cbHands.TabStop = false;
            this.cbHands.SelectedIndexChanged += new System.EventHandler(this.CbHands_SelectedIndexChanged);
            this.cbHands.DropDownClosed += new System.EventHandler(this.Combo_DropDownClosed);
            //
            // lblSecSound
            //
            this.lblSecSound.AutoSize = true;
            this.lblSecSound.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecSound.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecSound.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecSound.Name = "lblSecSound";
            this.lblSecSound.Text = "KLANG BEIM SELBERSPIELEN";
            //
            // cbSound
            //
            this.cbSound.BackColor = System.Drawing.Color.FromArgb(44, 49, 68);
            this.cbSound.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbSound.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbSound.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.cbSound.Items.AddRange(new object[] {
            "Klavier",
            "E-Piano",
            "Orgel",
            "Streicher",
            "Synthesizer"});
            this.cbSound.Name = "cbSound";
            this.cbSound.Size = new System.Drawing.Size(270, 28);
            this.cbSound.TabStop = false;
            this.cbSound.SelectedIndexChanged += new System.EventHandler(this.CbSound_SelectedIndexChanged);
            this.cbSound.DropDownClosed += new System.EventHandler(this.Combo_DropDownClosed);
            //
            // lblSecOut
            //
            this.lblSecOut.AutoSize = true;
            this.lblSecOut.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecOut.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecOut.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecOut.Name = "lblSecOut";
            this.lblSecOut.Text = "AUSGABEGERÄT";
            //
            // cbOut
            //
            this.cbOut.BackColor = System.Drawing.Color.FromArgb(44, 49, 68);
            this.cbOut.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbOut.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbOut.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.cbOut.Name = "cbOut";
            this.cbOut.Size = new System.Drawing.Size(270, 28);
            this.cbOut.TabStop = false;
            this.cbOut.SelectedIndexChanged += new System.EventHandler(this.CbOut_SelectedIndexChanged);
            this.cbOut.DropDownClosed += new System.EventHandler(this.Combo_DropDownClosed);
            //
            // vstRow
            //
            this.vstRow.Controls.Add(this.btnVst);
            this.vstRow.Controls.Add(this.btnVstEditor);
            this.vstRow.Margin = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.vstRow.Name = "vstRow";
            this.vstRow.Size = new System.Drawing.Size(270, 34);
            //
            // btnVst
            //
            this.btnVst.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnVst.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnVst.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnVst.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnVst.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btnVst.Name = "btnVst";
            this.btnVst.Size = new System.Drawing.Size(168, 32);
            this.btnVst.TabStop = false;
            this.btnVst.Text = "VST-Plugin laden …";
            this.btnVst.UseVisualStyleBackColor = false;
            this.btnVst.Click += new System.EventHandler(this.BtnVst_Click);
            //
            // btnVstEditor
            //
            this.btnVstEditor.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnVstEditor.Enabled = false;
            this.btnVstEditor.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnVstEditor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnVstEditor.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnVstEditor.Margin = new System.Windows.Forms.Padding(0);
            this.btnVstEditor.Name = "btnVstEditor";
            this.btnVstEditor.Size = new System.Drawing.Size(96, 32);
            this.btnVstEditor.TabStop = false;
            this.btnVstEditor.Text = "Editor";
            this.btnVstEditor.UseVisualStyleBackColor = false;
            this.btnVstEditor.Click += new System.EventHandler(this.BtnVstEditor_Click);
            //
            // lblSecIn
            //
            this.lblSecIn.AutoSize = true;
            this.lblSecIn.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecIn.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecIn.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecIn.Name = "lblSecIn";
            this.lblSecIn.Text = "MIDI-KEYBOARD (EINGANG)";
            //
            // cbIn
            //
            this.cbIn.BackColor = System.Drawing.Color.FromArgb(44, 49, 68);
            this.cbIn.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbIn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbIn.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.cbIn.Name = "cbIn";
            this.cbIn.Size = new System.Drawing.Size(270, 28);
            this.cbIn.TabStop = false;
            this.cbIn.SelectedIndexChanged += new System.EventHandler(this.CbIn_SelectedIndexChanged);
            this.cbIn.DropDownClosed += new System.EventHandler(this.Combo_DropDownClosed);
            //
            // lblSecVolume
            //
            this.lblSecVolume.AutoSize = true;
            this.lblSecVolume.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecVolume.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecVolume.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecVolume.Name = "lblSecVolume";
            this.lblSecVolume.Text = "LAUTSTÄRKE";
            //
            // volume
            //
            this.volume.Maximum = 100;
            this.volume.Name = "volume";
            this.volume.Size = new System.Drawing.Size(260, 45);
            this.volume.TabStop = false;
            this.volume.TickStyle = System.Windows.Forms.TickStyle.None;
            this.volume.Value = 80;
            this.volume.Scroll += new System.EventHandler(this.Volume_Scroll);
            this.volume.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Slider_MouseUp);
            //
            // lblSecFall
            //
            this.lblSecFall.AutoSize = true;
            this.lblSecFall.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecFall.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecFall.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecFall.Name = "lblSecFall";
            this.lblSecFall.Text = "FALLZEIT DER NOTEN";
            //
            // fall
            //
            this.fall.Maximum = 24;
            this.fall.Minimum = 4;
            this.fall.Name = "fall";
            this.fall.Size = new System.Drawing.Size(260, 45);
            this.fall.TabStop = false;
            this.fall.TickStyle = System.Windows.Forms.TickStyle.None;
            this.fall.Value = 8;
            this.fall.Scroll += new System.EventHandler(this.Fall_Scroll);
            this.fall.MouseUp += new System.Windows.Forms.MouseEventHandler(this.Slider_MouseUp);
            //
            // lblSecOptions
            //
            this.lblSecOptions.AutoSize = true;
            this.lblSecOptions.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecOptions.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecOptions.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecOptions.Name = "lblSecOptions";
            this.lblSecOptions.Text = "OPTIONEN";
            //
            // chkMetro
            //
            this.chkMetro.AutoSize = true;
            this.chkMetro.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.chkMetro.Name = "chkMetro";
            this.chkMetro.TabStop = false;
            this.chkMetro.Text = "Metronom";
            this.chkMetro.UseVisualStyleBackColor = true;
            this.chkMetro.CheckedChanged += new System.EventHandler(this.ChkMetro_CheckedChanged);
            //
            // chkNames
            //
            this.chkNames.AutoSize = true;
            this.chkNames.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.chkNames.Name = "chkNames";
            this.chkNames.TabStop = false;
            this.chkNames.Text = "Notennamen anzeigen";
            this.chkNames.UseVisualStyleBackColor = true;
            this.chkNames.CheckedChanged += new System.EventHandler(this.ChkNames_CheckedChanged);
            //
            // chkSheet
            //
            this.chkSheet.AutoSize = true;
            this.chkSheet.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.chkSheet.Name = "chkSheet";
            this.chkSheet.TabStop = false;
            this.chkSheet.Text = "Notenschrift anzeigen (F5)";
            this.chkSheet.UseVisualStyleBackColor = true;
            this.chkSheet.CheckedChanged += new System.EventHandler(this.ChkSheet_CheckedChanged);
            //
            // panelOct
            //
            this.panelOct.BackColor = System.Drawing.Color.FromArgb(32, 36, 50);
            this.panelOct.Controls.Add(this.lblOct);
            this.panelOct.Controls.Add(this.btnOctDown);
            this.panelOct.Controls.Add(this.btnOctUp);
            this.panelOct.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelOct.Name = "panelOct";
            this.panelOct.Size = new System.Drawing.Size(60, 576);
            //
            // lblOct
            //
            this.lblOct.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblOct.ForeColor = System.Drawing.Color.FromArgb(150, 158, 180);
            this.lblOct.Location = new System.Drawing.Point(2, 480);
            this.lblOct.Name = "lblOct";
            this.lblOct.Size = new System.Drawing.Size(56, 18);
            this.lblOct.Text = "Oktave";
            this.lblOct.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // btnOctDown
            //
            this.btnOctDown.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnOctDown.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnOctDown.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnOctDown.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOctDown.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnOctDown.Location = new System.Drawing.Point(2, 502);
            this.btnOctDown.Name = "btnOctDown";
            this.btnOctDown.Size = new System.Drawing.Size(27, 64);
            this.btnOctDown.TabStop = false;
            this.btnOctDown.Text = "<";
            this.btnOctDown.UseVisualStyleBackColor = false;
            this.btnOctDown.Click += new System.EventHandler(this.BtnOctDown_Click);
            //
            // btnOctUp
            //
            this.btnOctUp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnOctUp.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnOctUp.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnOctUp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnOctUp.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnOctUp.Location = new System.Drawing.Point(31, 502);
            this.btnOctUp.Name = "btnOctUp";
            this.btnOctUp.Size = new System.Drawing.Size(27, 64);
            this.btnOctUp.TabStop = false;
            this.btnOctUp.Text = ">";
            this.btnOctUp.UseVisualStyleBackColor = false;
            this.btnOctUp.Click += new System.EventHandler(this.BtnOctUp_Click);            //
            // lblSecKeys
            //
            this.lblSecKeys.AutoSize = true;
            this.lblSecKeys.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecKeys.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecKeys.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecKeys.Name = "lblSecKeys";
            this.lblSecKeys.Text = "TASTATUR";
            //
            // cbKeys
            //
            this.cbKeys.BackColor = System.Drawing.Color.FromArgb(44, 49, 68);
            this.cbKeys.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbKeys.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbKeys.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.cbKeys.Items.AddRange(new object[] {
            "An Stück angepasst",
            "49 Tasten (C – C)",
            "61 Tasten (C – C)",
            "88 Tasten (A – C)"});
            this.cbKeys.Name = "cbKeys";
            this.cbKeys.Size = new System.Drawing.Size(270, 28);
            this.cbKeys.TabStop = false;
            this.cbKeys.SelectedIndexChanged += new System.EventHandler(this.CbKeys_SelectedIndexChanged);
            this.cbKeys.DropDownClosed += new System.EventHandler(this.Combo_DropDownClosed);
            //
            // lblSecLight
            //
            this.lblSecLight.AutoSize = true;
            this.lblSecLight.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecLight.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecLight.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecLight.Name = "lblSecLight";
            this.lblSecLight.Text = "LIGHT GUIDE (NI KEYBOARD)";
            //
            // cbLight
            //
            this.cbLight.BackColor = System.Drawing.Color.FromArgb(44, 49, 68);
            this.cbLight.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbLight.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cbLight.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.cbLight.Items.AddRange(new object[] {
            "Aus",
            "Kontrol S-Serie MK3 (Light Guide)"});
            this.cbLight.Name = "cbLight";
            this.cbLight.Size = new System.Drawing.Size(270, 28);
            this.cbLight.TabStop = false;
            this.cbLight.SelectedIndexChanged += new System.EventHandler(this.CbLight_SelectedIndexChanged);
            this.cbLight.DropDownClosed += new System.EventHandler(this.Combo_DropDownClosed);
            //
            // lblLightStatus
            //
            this.lblLightStatus.AutoSize = true;
            this.lblLightStatus.ForeColor = System.Drawing.Color.FromArgb(150, 158, 180);
            this.lblLightStatus.Margin = new System.Windows.Forms.Padding(0, 3, 0, 0);
            this.lblLightStatus.MaximumSize = new System.Drawing.Size(270, 0);
            this.lblLightStatus.Name = "lblLightStatus";
            this.lblLightStatus.Text = "";
            //
            // chkPcKeys
            //
            this.chkPcKeys.AutoSize = true;
            this.chkPcKeys.Checked = true;
            this.chkPcKeys.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkPcKeys.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.chkPcKeys.Name = "chkPcKeys";
            this.chkPcKeys.TabStop = false;
            this.chkPcKeys.Text = "PC-Tastatur als Klavier";
            this.chkPcKeys.UseVisualStyleBackColor = true;
            this.chkPcKeys.CheckedChanged += new System.EventHandler(this.ChkPcKeys_CheckedChanged);
            //
            // lblSecLoop
            //
            this.lblSecLoop.AutoSize = true;
            this.lblSecLoop.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecLoop.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecLoop.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecLoop.Name = "lblSecLoop";
            this.lblSecLoop.Text = "ABSCHNITT WIEDERHOLEN";
            //
            // loopRow
            //
            this.loopRow.Controls.Add(this.btnLoopA);
            this.loopRow.Controls.Add(this.btnLoopB);
            this.loopRow.Controls.Add(this.btnLoopClear);
            this.loopRow.Margin = new System.Windows.Forms.Padding(0);
            this.loopRow.Name = "loopRow";
            this.loopRow.Size = new System.Drawing.Size(270, 38);
            this.loopRow.WrapContents = false;
            //
            // btnLoopA
            //
            this.btnLoopA.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnLoopA.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnLoopA.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLoopA.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnLoopA.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btnLoopA.Name = "btnLoopA";
            this.btnLoopA.Size = new System.Drawing.Size(80, 32);
            this.btnLoopA.TabStop = false;
            this.btnLoopA.Text = "A setzen";
            this.btnLoopA.UseVisualStyleBackColor = false;
            this.btnLoopA.Click += new System.EventHandler(this.BtnLoopA_Click);
            //
            // btnLoopB
            //
            this.btnLoopB.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnLoopB.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnLoopB.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLoopB.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnLoopB.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btnLoopB.Name = "btnLoopB";
            this.btnLoopB.Size = new System.Drawing.Size(80, 32);
            this.btnLoopB.TabStop = false;
            this.btnLoopB.Text = "B setzen";
            this.btnLoopB.UseVisualStyleBackColor = false;
            this.btnLoopB.Click += new System.EventHandler(this.BtnLoopB_Click);
            //
            // btnLoopClear
            //
            this.btnLoopClear.BackColor = System.Drawing.Color.FromArgb(52, 58, 80);
            this.btnLoopClear.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(80, 88, 115);
            this.btnLoopClear.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnLoopClear.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.btnLoopClear.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            this.btnLoopClear.Name = "btnLoopClear";
            this.btnLoopClear.Size = new System.Drawing.Size(80, 32);
            this.btnLoopClear.TabStop = false;
            this.btnLoopClear.Text = "Löschen";
            this.btnLoopClear.UseVisualStyleBackColor = false;
            this.btnLoopClear.Click += new System.EventHandler(this.BtnLoopClear_Click);
            //
            // chkLoop
            //
            this.chkLoop.AutoSize = true;
            this.chkLoop.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.chkLoop.Name = "chkLoop";
            this.chkLoop.TabStop = false;
            this.chkLoop.Text = "Abschnitt A–B wiederholen";
            this.chkLoop.UseVisualStyleBackColor = true;
            this.chkLoop.CheckedChanged += new System.EventHandler(this.ChkLoop_CheckedChanged);
            //
            // lblLoop
            //
            this.lblLoop.AutoSize = true;
            this.lblLoop.ForeColor = System.Drawing.Color.FromArgb(160, 168, 185);
            this.lblLoop.Name = "lblLoop";
            this.lblLoop.Text = "Kein Abschnitt gesetzt";
            //
            // lblSecTracks
            //
            this.lblSecTracks.AutoSize = true;
            this.lblSecTracks.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold);
            this.lblSecTracks.ForeColor = System.Drawing.Color.FromArgb(120, 190, 255);
            this.lblSecTracks.Margin = new System.Windows.Forms.Padding(0, 10, 0, 3);
            this.lblSecTracks.Name = "lblSecTracks";
            this.lblSecTracks.Text = "SPUREN / HÄNDE";
            //
            // trackPanel
            //
            this.trackPanel.AutoSize = true;
            this.trackPanel.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.trackPanel.Margin = new System.Windows.Forms.Padding(0);
            this.trackPanel.Name = "trackPanel";
            this.trackPanel.Size = new System.Drawing.Size(270, 0);
            this.trackPanel.WrapContents = false;
            //
            // sheet
            //
            this.sheet.BackColor = System.Drawing.Color.FromArgb(17, 20, 31);
            this.sheet.Dock = System.Windows.Forms.DockStyle.Top;
            this.sheet.Name = "sheet";
            this.sheet.Size = new System.Drawing.Size(990, 150);
            this.sheet.TabIndex = 3;
            this.sheet.Visible = false;
            //
            // view
            //
            this.view.BackColor = System.Drawing.Color.FromArgb(11, 13, 20);
            this.view.Dock = System.Windows.Forms.DockStyle.Fill;
            this.view.Name = "view";
            this.view.Size = new System.Drawing.Size(990, 728);
            this.view.TabIndex = 0;
            this.view.KeyPressed += new System.Action<int>(this.View_KeyPressed);
            this.view.KeyReleased += new System.Action<int>(this.View_KeyReleased);
            //
            // Form1
            //
            this.AllowDrop = true;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(24, 27, 38);
            this.ClientSize = new System.Drawing.Size(1280, 780);
            this.Controls.Add(this.view);
            this.Controls.Add(this.panelOct);
            this.Controls.Add(this.sheet);
            this.Controls.Add(this.panelSide);
            this.Controls.Add(this.panelTop);
            this.ForeColor = System.Drawing.Color.FromArgb(226, 230, 240);
            this.KeyPreview = true;
            this.MinimumSize = new System.Drawing.Size(900, 560);
            this.Name = "Form1";
            this.Text = "Synthypia";
            this.DragDrop += new System.Windows.Forms.DragEventHandler(this.Form1_DragDrop);
            this.DragEnter += new System.Windows.Forms.DragEventHandler(this.Form1_DragEnter);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.OnFormKeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.OnFormKeyUp);
            this.panelTop.ResumeLayout(false);
            this.panelTop.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.seek)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.tempo)).EndInit();
            this.panelSide.ResumeLayout(false);
            this.panelSide.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.volume)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.fall)).EndInit();
            this.loopRow.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer uiTimer;
        private System.Windows.Forms.Panel panelTop;
        private Synthypia.MetroSlider seek;
        private System.Windows.Forms.Label lblTime;
        private Synthypia.MetroSlider tempo;
        private System.Windows.Forms.Label lblTempo;
        private Synthypia.MetroButton btnStop;
        private Synthypia.MetroButton btnPlay;
        private Synthypia.MetroButton btnOpen;
        private System.Windows.Forms.FlowLayoutPanel panelSide;
        private System.Windows.Forms.Label lblSecMode;
        private Synthypia.MetroComboBox cbMode;
        private Synthypia.MetroComboBox cbHands;
        private System.Windows.Forms.Label lblSecSound;
        private Synthypia.MetroComboBox cbSound;
        private System.Windows.Forms.Label lblSecOut;
        private Synthypia.MetroComboBox cbOut;
        private System.Windows.Forms.FlowLayoutPanel vstRow;
        private Synthypia.MetroButton btnVst;
        private Synthypia.MetroButton btnVstEditor;
        private System.Windows.Forms.Label lblSecIn;
        private Synthypia.MetroComboBox cbIn;
        private System.Windows.Forms.Label lblSecVolume;
        private Synthypia.MetroSlider volume;
        private System.Windows.Forms.Label lblSecFall;
        private Synthypia.MetroSlider fall;
        private System.Windows.Forms.Label lblSecOptions;
        private Synthypia.MetroCheckBox chkMetro;
        private Synthypia.MetroCheckBox chkNames;
        private System.Windows.Forms.Label lblSecKeys;
        private Synthypia.MetroComboBox cbKeys;
        private System.Windows.Forms.Label lblSecLight;
        private Synthypia.MetroComboBox cbLight;
        private System.Windows.Forms.Label lblLightStatus;
        private System.Windows.Forms.Panel panelOct;
        private System.Windows.Forms.Label lblOct;
        private Synthypia.MetroButton btnOctDown;
        private Synthypia.MetroButton btnOctUp;
        private Synthypia.MetroCheckBox chkPcKeys;
        private System.Windows.Forms.Label lblSecLoop;
        private System.Windows.Forms.FlowLayoutPanel loopRow;
        private Synthypia.MetroButton btnLoopA;
        private Synthypia.MetroButton btnLoopB;
        private Synthypia.MetroButton btnLoopClear;
        private Synthypia.MetroCheckBox chkLoop;
        private System.Windows.Forms.Label lblLoop;
        private System.Windows.Forms.Label lblSecTracks;
        private System.Windows.Forms.FlowLayoutPanel trackPanel;
        private Synthypia.MetroCheckBox chkSheet;
        private SheetView sheet;
        private PianoView view;
    }
}
