using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace REPOMenuInstaller
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox _folder = new TextBox { ReadOnly = true, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
        private readonly Button _browse = new Button { Text = "Browse...", AutoSize = true };
        private readonly Button _install = new Button { Text = "Install / Update", AutoSize = true };
        private readonly Button _uninstall = new Button { Text = "Uninstall", AutoSize = true };
        private readonly Button _close = new Button { Text = "Close", AutoSize = true };
        private readonly TextBox _log = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill, BackColor = SystemColors.Window, Font = new Font(FontFamily.GenericMonospace, 9f),
        };
        private readonly string _autoAction;
        private string _game;

        internal MainForm(string gameArg, string autoAction)
        {
            _autoAction = autoAction;
            Text = Payload.Online ? "REPOMenu online installer" : "REPOMenu " + Program.Version + " installer";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 380);
            MinimumSize = new Size(540, 320);
            Font = SystemFonts.MessageBoxFont;

            var intro = new Label
            {
                AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 0, 0, 6),
                Text = "Installs the REPOMenu mod menu into R.E.P.O. (press F2 in game to open it).\n" +
                       "It installs BepInEx 5 as well when the game does not have it yet, and leaves any BepInEx\n" +
                       "and other mods you already have untouched.\n" +
                       (Payload.Online
                           ? "Install / Update downloads the newest version from GitHub every time, so this installer never gets old."
                           : "This installer has version " + Program.Version + " built in and needs no internet."),
            };
            var folderRow = new TableLayoutPanel { ColumnCount = 3, Dock = DockStyle.Fill, AutoSize = true };
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            folderRow.Controls.Add(new Label { Text = "Game folder:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            folderRow.Controls.Add(_folder, 1, 0);
            folderRow.Controls.Add(_browse, 2, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            buttons.Controls.AddRange(new Control[] { _install, _uninstall, _close });

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(intro, 0, 0);
            layout.Controls.Add(folderRow, 0, 1);
            layout.Controls.Add(_log, 0, 2);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);
            AcceptButton = _install;
            CancelButton = _close;

            _browse.Click += (s, e) => Browse();
            _install.Click += (s, e) => Run("install");
            _uninstall.Click += (s, e) => Run("uninstall");
            _close.Click += (s, e) => Close();
            Shown += (s, e) =>
            {
                if (_autoAction != null) Run(_autoAction);
            };

            UseFolder(gameArg ?? GameLocator.FindGameFolder(), fromSteam: gameArg == null);
        }

        private void UseFolder(string folder, bool fromSteam)
        {
            _game = GameLocator.ToGameFolder(folder);
            if (_game == null)
            {
                _folder.Text = "";
                Log(folder == null
                    ? "Could not find R.E.P.O. through Steam. Click Browse and pick the game folder (the one with REPO.exe)."
                    : "That folder does not contain R.E.P.O. (REPO.exe).");
            }
            else
            {
                _folder.Text = _game;
                Log((fromSteam ? "Found the game: " : "Game folder: ") + _game);
                Log(ModInstaller.HasBepInEx(_game) ? "BepInEx is already installed here." : "BepInEx is not installed yet; it will be added.");
                Log(ModInstaller.IsInstalled(_game) ? "REPOMenu is installed. Install / Update refreshes it." : "REPOMenu is not installed yet.");
            }
            _install.Enabled = _uninstall.Enabled = _game != null;
        }

        private void Browse()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Pick the R.E.P.O. folder (the one with REPO.exe)" })
            {
                if (dialog.ShowDialog(this) == DialogResult.OK) UseFolder(dialog.SelectedPath, fromSteam: false);
            }
        }

        private async void Run(string action)
        {
            if (_game == null) return;
            if (!ModInstaller.CanWrite(_game))
            {
                if (Program.IsElevated())
                {
                    Log("Error: cannot write to " + _game);
                    return;
                }
                Log("The game folder needs admin permission. Asking Windows...");
                if (RelaunchElevated(action)) Close();
                else Log("Admin permission was refused, nothing was changed.");
                return;
            }
            UseWaitCursor = true;
            _install.Enabled = _uninstall.Enabled = false;
            try
            {
                if (action == "install")
                {
                    ModInstaller.RefuseWhileRunning();          // before downloading anything
                    byte[] downloaded = Payload.Online ? await Task.Run(() => Payload.Fetch(Log)) : null;
                    using (Stream payload = Payload.Open(downloaded))
                        ModInstaller.Install(_game, Log, payload);
                }
                else
                {
                    ModInstaller.Uninstall(_game, Log);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException)
            {
                Log("Error: " + ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                _install.Enabled = _uninstall.Enabled = true;
                UseFolder(_game, fromSteam: false);
            }
        }

        private bool RelaunchElevated(string action)
        {
            var psi = new ProcessStartInfo(Application.ExecutablePath,
                "--game \"" + _game + "\" --" + action)
            { UseShellExecute = true, Verb = "runas" };
            try
            {
                Process.Start(psi)?.Dispose();
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
        }

        // Also called from the download, which runs off the window's thread.
        private void Log(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), line);
                return;
            }
            _log.AppendText(line + Environment.NewLine);
        }
    }
}
