using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EqualizerProfileSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainForm(Settings.Load())); }
            catch (Exception ex) { MessageBox.Show(ex.Message + "\nSettings: " + Settings.Home, "Startup failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    public sealed class MainForm : Form
    {
        private readonly Settings settings;
        private readonly ProfileService service = new ProfileService();
        private readonly FlowLayoutPanel profiles = new FlowLayoutPanel();
        private readonly Label status = new Label();
        private readonly TextBox target = new TextBox();
        private readonly TextBox folder = new TextBox();
        private readonly Timer timer = new Timer { Interval = 1000 };
        private string signature = "";
        private bool applying;

        public MainForm(Settings settings)
        {
            this.settings = settings;
            Text = "Equalizer Profile Switcher";
            Font = new Font("Segoe UI", 10);
            Size = new Size(590, 620);
            MinimumSize = new Size(460, 420);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = settings.AlwaysOnTop;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, Padding = new Padding(14) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(new Label { Text = "Equalizer APO configuration", AutoSize = true });
            target.Text = settings.ConfigPath;
            target.ReadOnly = true;
            layout.Controls.Add(PathRow(target, "Browse…", ChooseConfig));
            layout.Controls.Add(new Label { Text = "Profiles folder — one .txt file per button", AutoSize = true });
            folder.Text = settings.ProfilesDirectory;
            folder.ReadOnly = true;
            layout.Controls.Add(PathRow(folder, "Browse…", ChooseFolder));
            var options = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
            var top = new CheckBox { Text = "Always on top", Checked = settings.AlwaysOnTop, AutoSize = true };
            top.CheckedChanged += delegate { settings.AlwaysOnTop = TopMost = top.Checked; SaveSettings(); };
            var minimize = new CheckBox { Text = "Minimize after applying", Checked = settings.MinimizeAfterApply, AutoSize = true };
            minimize.CheckedChanged += delegate { settings.MinimizeAfterApply = minimize.Checked; SaveSettings(); };
            options.Controls.Add(top); options.Controls.Add(minimize);
            layout.Controls.Add(options);
            var open = new Button { Text = "Open profiles folder", AutoSize = true };
            open.Click += delegate { try { Process.Start("explorer.exe", "\"" + settings.ProfilesDirectory + "\""); } catch (Exception ex) { ShowError(ex); } };
            layout.Controls.Add(open);
            profiles.Dock = DockStyle.Fill;
            profiles.AutoScroll = true;
            profiles.FlowDirection = FlowDirection.TopDown;
            profiles.WrapContents = false;
            profiles.Resize += delegate { foreach (Control button in profiles.Controls) button.Width = Math.Max(100, profiles.ClientSize.Width - 28); };
            layout.Controls.Add(profiles);
            status.AutoSize = true; status.MaximumSize = new Size(520, 0); status.Text = "Select a configuration and a profile. Changes are backed up.";
            layout.Controls.Add(status);
            Controls.Add(layout);
            timer.Tick += delegate { if (!applying) RefreshProfiles(); };
            RefreshProfiles(); timer.Start();
        }

        private Control PathRow(TextBox text, string caption, EventHandler action)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            text.Dock = DockStyle.Fill;
            var button = new Button { Text = caption, AutoSize = true }; button.Click += action;
            row.Controls.Add(text); row.Controls.Add(button);
            return row;
        }

        private void ChooseConfig(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Filter = "Equalizer APO configuration (*.txt)|*.txt", CheckFileExists = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) { settings.ConfigPath = target.Text = dialog.FileName; signature = ""; SaveSettings(); }
        }

        private void ChooseFolder(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { SelectedPath = settings.ProfilesDirectory, Description = "Select a folder containing your APO profiles" })
                if (dialog.ShowDialog(this) == DialogResult.OK) { settings.ProfilesDirectory = folder.Text = dialog.SelectedPath; signature = ""; SaveSettings(); RefreshProfiles(); }
        }

        private void RefreshProfiles()
        {
            try
            {
                string[] files = service.Discover(settings.ProfilesDirectory);
                string active = service.CurrentProfile(settings.ConfigPath);
                string next = string.Join("|", files) + "|" + active;
                if (next == signature) return;
                signature = next;
                while (profiles.Controls.Count > 0) profiles.Controls[0].Dispose();
                foreach (string file in files)
                {
                    string path = file;
                    bool selected = string.Equals(path, active, StringComparison.OrdinalIgnoreCase);
                    var button = new Button { Text = (selected ? "✓  " : "") + Path.GetFileNameWithoutExtension(path), Height = 48, Width = Math.Max(100, profiles.ClientSize.Width - 28), TextAlign = ContentAlignment.MiddleLeft, AccessibleName = Path.GetFileNameWithoutExtension(path) };
                    button.Click += async delegate
                    {
                        applying = true; Enabled = false;
                        try
                        {
                            await service.ApplyAsync(settings.ConfigPath, path);
                            status.Text = "Applied: " + Path.GetFileNameWithoutExtension(path);
                            signature = "";
                            if (settings.MinimizeAfterApply) WindowState = FormWindowState.Minimized;
                        }
                        catch (Exception ex) { ShowError(ex); }
                        finally { applying = false; Enabled = true; RefreshProfiles(); }
                    };
                    profiles.Controls.Add(button);
                }
                if (files.Length == 0) status.Text = "No profiles found. Add .txt files to the profiles folder.";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        }

        private void SaveSettings() { try { settings.Save(); } catch (Exception ex) { ShowError(ex); } }
        private void ShowError(Exception ex)
        {
            status.Text = "Could not apply or save changes.";
            MessageBox.Show(this, ex.Message + "\n\nFor protected APO folders, grant your account write access or run this tool as administrator.", "Equalizer Profile Switcher", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }
}
