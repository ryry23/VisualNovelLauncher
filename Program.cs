using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VisualNovelLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class AppProfile
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool ChangeResolution { get; set; }
    public bool ChangeRefreshRate { get; set; } = true;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public int RefreshRate { get; set; } = 60;
    public int RestoreDelayMs { get; set; } = 500;
}

internal static class ProfileStore
{
    public static string FilePath => System.IO.Path.Combine(AppContext.BaseDirectory, "profiles.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public static List<AppProfile> Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<List<AppProfile>>(File.ReadAllText(FilePath), Options) ?? []
                : [];
        }
        catch (Exception ex)
        {
            MessageBox.Show($"profiles.json を読み込めませんでした。\n\n{ex.Message}", "Visual Novel Launcher", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return [];
        }
    }

    public static void Save(List<AppProfile> profiles) => File.WriteAllText(FilePath, JsonSerializer.Serialize(profiles, Options));
}

public sealed class MainForm : Form
{
    private readonly ListView list = new();
    private readonly ImageList appIcons = new() { ImageSize = new Size(32, 32), ColorDepth = ColorDepth.Depth32Bit };
    private readonly Button addButton = new() { Text = "+", Width = 44, Height = 34 };
    private readonly Button removeButton = new() { Text = "−", Width = 44, Height = 34 };
    private readonly Button editButton = new() { Text = "編集", Width = 74, Height = 34 };
    private readonly Button launchButton = new() { Text = "起動", Width = 100, Height = 34 };
    private readonly Label status = new() { AutoSize = true, Text = "アプリを選択してください" };
    private readonly List<AppProfile> profiles;
    private bool running;

    public MainForm()
    {
        Text = "Visual Novel Launcher";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(680, 380);
        Size = new Size(840, 470);
        Font = new Font("Segoe UI", 10F);
        AllowDrop = true;

        profiles = ProfileStore.Load();
        BuildUi();
        RefreshList();
    }

    private void BuildUi()
    {
        var header = new Label { Text = "アプリごとの表示設定", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(0, 7, 0, 0) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(12, 10, 12, 6), WrapContents = false };
        top.Controls.Add(header);
        top.Controls.Add(new Label { Width = 18 });
        top.Controls.Add(addButton);
        top.Controls.Add(removeButton);
        top.Controls.Add(editButton);

        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.HideSelection = false;
        list.MultiSelect = false;
        list.AllowDrop = true;
        list.SmallImageList = appIcons;
        list.Columns.Add("名前", 190);
        list.Columns.Add("表示モード", 190);
        list.Columns.Add("アプリ", 470);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 66, ColumnCount = 2, Padding = new Padding(12, 10, 12, 10) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(status, 0, 0);
        bottom.Controls.Add(launchButton, 1, 0);
        status.Anchor = AnchorStyles.Left;

        Controls.Add(list);
        Controls.Add(bottom);
        Controls.Add(top);

        addButton.Click += (_, _) => AddProfile();
        removeButton.Click += (_, _) => RemoveProfile();
        editButton.Click += (_, _) => EditProfile();
        launchButton.Click += async (_, _) => await LaunchSelectedAsync();
        list.DoubleClick += async (_, _) => await LaunchSelectedAsync();
        list.SelectedIndexChanged += (_, _) => UpdateButtons();
        DragEnter += HandleDragEnter;
        DragDrop += HandleDragDrop;
        list.DragEnter += HandleDragEnter;
        list.DragDrop += HandleDragDrop;
        FormClosing += (_, e) => { if (running) { e.Cancel = true; MessageBox.Show("起動したアプリを終了してから閉じてください。"); } };
        UpdateButtons();
    }

    private AppProfile? Selected => list.SelectedIndices.Count == 1 ? profiles[list.SelectedIndices[0]] : null;

    private void RefreshList(int selectIndex = -1)
    {
        list.BeginUpdate();
        list.Items.Clear();
        appIcons.Images.Clear();
        foreach (var profile in profiles)
        {
            var mode = !profile.ChangeResolution && !profile.ChangeRefreshRate
                ? "システム設定を使用"
                : $"{(profile.ChangeResolution ? $"{profile.Width}×{profile.Height}" : "システムの解像度")} / "
                  + $"{(profile.ChangeRefreshRate ? $"{profile.RefreshRate} Hz" : "システムのHz")}";
            var item = new ListViewItem([profile.Name, mode, profile.Path]);
            try
            {
                if (File.Exists(profile.Path))
                {
                    using var icon = Icon.ExtractAssociatedIcon(profile.Path);
                    if (icon is not null)
                    {
                        appIcons.Images.Add(icon.ToBitmap());
                        item.ImageIndex = appIcons.Images.Count - 1;
                    }
                }
            }
            catch { }
            list.Items.Add(item);
        }
        list.EndUpdate();
        if (selectIndex >= 0 && selectIndex < list.Items.Count)
            list.Items[selectIndex].Selected = true;
        UpdateButtons();
    }

    private void AddProfile()
    {
        using var dialog = new ProfileDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        profiles.Add(dialog.Profile);
        ProfileStore.Save(profiles);
        RefreshList(profiles.Count - 1);
    }

    private static string[] GetDroppedExecutables(DragEventArgs e)
    {
        if (!e.Data!.GetDataPresent(DataFormats.FileDrop)) return [];
        return ((string[])e.Data.GetData(DataFormats.FileDrop)!)
            .Where(path => string.Equals(System.IO.Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            .Where(File.Exists)
            .ToArray();
    }

    private void HandleDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = !running && GetDroppedExecutables(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private void HandleDragDrop(object? sender, DragEventArgs e)
    {
        foreach (var path in GetDroppedExecutables(e))
        {
            var initial = new AppProfile
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(path),
                Path = path,
                ChangeResolution = false,
                ChangeRefreshRate = false
            };
            using var dialog = new ProfileDialog(initial);
            if (dialog.ShowDialog(this) != DialogResult.OK) continue;
            profiles.Add(dialog.Profile);
        }
        ProfileStore.Save(profiles);
        RefreshList(profiles.Count - 1);
    }

    private void EditProfile()
    {
        var profile = Selected;
        if (profile is null) return;
        var index = list.SelectedIndices[0];
        using var dialog = new ProfileDialog(profile);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        profiles[index] = dialog.Profile;
        ProfileStore.Save(profiles);
        RefreshList(index);
    }

    private void RemoveProfile()
    {
        var profile = Selected;
        if (profile is null) return;
        if (MessageBox.Show($"「{profile.Name}」を削除しますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        profiles.RemoveAt(list.SelectedIndices[0]);
        ProfileStore.Save(profiles);
        RefreshList();
    }

    private async Task LaunchSelectedAsync()
    {
        var profile = Selected;
        if (profile is null || running) return;
        if (!File.Exists(profile.Path))
        {
            MessageBox.Show("アプリが見つかりません。設定を編集してください。", "起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        running = true;
        UpdateButtons();
        var changesDisplayMode = profile.ChangeResolution || profile.ChangeRefreshRate;
        var original = DisplayMode.GetCurrent();
        try
        {
            if (changesDisplayMode)
            {
                status.Text = "表示モードを切り替えています…";
                DisplayMode.ChangeTo(
                    profile.ChangeResolution ? profile.Width : 0,
                    profile.ChangeResolution ? profile.Height : 0,
                    profile.ChangeRefreshRate ? profile.RefreshRate : 0);
            }

            var start = new ProcessStartInfo(profile.Path)
            {
                WorkingDirectory = System.IO.Path.GetDirectoryName(profile.Path) ?? AppContext.BaseDirectory,
                Arguments = profile.Arguments,
                UseShellExecute = true
            };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("プロセスを開始できませんでした。");
            status.Text = $"{profile.Name} を実行中";
            await process.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "起動エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            if (changesDisplayMode)
            {
                status.Text = "元の表示モードへ戻しています…";
                if (profile.RestoreDelayMs > 0) await Task.Delay(profile.RestoreDelayMs);
                try { DisplayMode.Restore(original); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "復帰エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            running = false;
            status.Text = "完了";
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        var selected = Selected is not null;
        addButton.Enabled = !running;
        removeButton.Enabled = selected && !running;
        editButton.Enabled = selected && !running;
        launchButton.Enabled = selected && !running;
    }
}

public sealed class ProfileDialog : Form
{
    private readonly TextBox nameBox = new();
    private readonly TextBox pathBox = new();
    private readonly TextBox argumentsBox = new();
    private readonly CheckBox resolutionCheck = new() { Text = "解像度を変更する", AutoSize = true };
    private readonly CheckBox refreshRateCheck = new() { Text = "リフレッシュレートを変更する", Checked = false, AutoSize = true };
    private readonly NumericUpDown widthBox = new() { Minimum = 320, Maximum = 16384, Value = 1920 };
    private readonly NumericUpDown heightBox = new() { Minimum = 200, Maximum = 16384, Value = 1080 };
    private readonly NumericUpDown hzBox = new() { Minimum = 23, Maximum = 1000, Value = 60 };
    private readonly NumericUpDown delayBox = new() { Minimum = 0, Maximum = 10000, Increment = 100, Value = 500 };
    private readonly PictureBox iconPreview = new() { Size = new Size(36, 36), SizeMode = PictureBoxSizeMode.CenterImage, Margin = new Padding(8, 2, 0, 0) };
    public AppProfile Profile { get; private set; } = new();

    public ProfileDialog(AppProfile? source = null)
    {
        Text = source is null ? "アプリを追加" : "設定を編集";
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScroll = true;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(650, 460);
        ClientSize = new Size(720, 520);
        Font = new Font("Segoe UI", 10F);
        BuildUi();

        if (source is not null)
        {
            nameBox.Text = source.Name; pathBox.Text = source.Path; argumentsBox.Text = source.Arguments;
            resolutionCheck.Checked = source.ChangeResolution; widthBox.Value = source.Width; heightBox.Value = source.Height;
            refreshRateCheck.Checked = source.ChangeRefreshRate;
            hzBox.Value = source.RefreshRate; delayBox.Value = source.RestoreDelayMs;
        }
        UpdateResolutionFields();
        UpdateRefreshRateField();
    }

    private void BuildUi()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(16), ColumnCount = 3, RowCount = 9 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        for (var i = 0; i < 8; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

        AddRow(table, 0, "名前", nameBox);
        AddRow(table, 1, "アプリ", pathBox);
        var pathActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
        var browse = new Button { Text = "参照…", Width = 82, Height = 34, Margin = new Padding(4) };
        pathActions.Controls.Add(browse);
        pathActions.Controls.Add(iconPreview);
        table.Controls.Add(pathActions, 2, 1);
        AddRow(table, 2, "引数", argumentsBox);
        table.Controls.Add(resolutionCheck, 1, 3);
        table.SetColumnSpan(resolutionCheck, 2);

        var resolutionPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        widthBox.Width = 100; heightBox.Width = 100;
        resolutionPanel.Controls.Add(widthBox); resolutionPanel.Controls.Add(new Label { Text = "×", AutoSize = true, Margin = new Padding(8, 7, 8, 0) }); resolutionPanel.Controls.Add(heightBox);
        AddRow(table, 4, "解像度", resolutionPanel);
        table.Controls.Add(refreshRateCheck, 1, 5);
        table.SetColumnSpan(refreshRateCheck, 2);
        AddRow(table, 6, "リフレッシュレート", hzBox);
        AddRow(table, 7, "復帰待機 (ms)", delayBox);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 8, 0, 4) };
        var ok = new Button { Text = "保存", Width = 100, Height = 34, DialogResult = DialogResult.None };
        var cancel = new Button { Text = "キャンセル", Width = 110, Height = 34, DialogResult = DialogResult.Cancel };
        buttons.Controls.Add(ok); buttons.Controls.Add(cancel);
        table.Controls.Add(buttons, 0, 8); table.SetColumnSpan(buttons, 3);
        Controls.Add(table);

        browse.Click += (_, _) => Browse();
        pathBox.TextChanged += (_, _) =>
        {
            UpdateDefaultName();
            UpdateIconPreview();
        };
        resolutionCheck.CheckedChanged += (_, _) => UpdateResolutionFields();
        refreshRateCheck.CheckedChanged += (_, _) => UpdateRefreshRateField();
        ok.Click += (_, _) => SaveAndClose();
        AcceptButton = ok; CancelButton = cancel;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill; control.Margin = new Padding(4);
        table.Controls.Add(control, 1, row);
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        pathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(nameBox.Text)) nameBox.Text = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private void UpdateIconPreview()
    {
        var oldImage = iconPreview.Image;
        iconPreview.Image = null;
        oldImage?.Dispose();
        try
        {
            if (!File.Exists(pathBox.Text)) return;
            using var icon = Icon.ExtractAssociatedIcon(pathBox.Text);
            if (icon is not null) iconPreview.Image = icon.ToBitmap();
        }
        catch { }
    }

    private void UpdateDefaultName()
    {
        if (!string.IsNullOrWhiteSpace(nameBox.Text) || !File.Exists(pathBox.Text)) return;
        nameBox.Text = System.IO.Path.GetFileNameWithoutExtension(pathBox.Text);
    }

    private void UpdateResolutionFields() => widthBox.Enabled = heightBox.Enabled = resolutionCheck.Checked;

    private void UpdateRefreshRateField() => hzBox.Enabled = refreshRateCheck.Checked;

    private void SaveAndClose()
    {
        if (string.IsNullOrWhiteSpace(nameBox.Text) || !File.Exists(pathBox.Text))
        {
            MessageBox.Show("名前と有効なEXEを指定してください。", "入力確認", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Profile = new AppProfile
        {
            Name = nameBox.Text.Trim(), Path = pathBox.Text.Trim(), Arguments = argumentsBox.Text,
            ChangeResolution = resolutionCheck.Checked, Width = (int)widthBox.Value, Height = (int)heightBox.Value,
            ChangeRefreshRate = refreshRateCheck.Checked, RefreshRate = (int)hzBox.Value, RestoreDelayMs = (int)delayBox.Value
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}

internal static class DisplayMode
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public short SpecVersion, DriverVersion, Size, DriverExtra;
        public int Fields, PositionX, PositionY, DisplayOrientation, DisplayFixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public short LogPixels;
        public int BitsPerPel, PelsWidth, PelsHeight, DisplayFlags, DisplayFrequency;
        public int ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Ansi)] private static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DevMode mode);
    [DllImport("user32.dll", CharSet = CharSet.Ansi)] private static extern int ChangeDisplaySettings(ref DevMode mode, int flags);

    public static DevMode GetCurrent()
    {
        var mode = new DevMode { Size = (short)Marshal.SizeOf<DevMode>() };
        if (!EnumDisplaySettings(null, -1, ref mode)) throw new InvalidOperationException("現在の表示モードを取得できません。");
        return mode;
    }

    public static void ChangeTo(int width, int height, int hz)
    {
        var mode = GetCurrent();
        mode.Fields = 0;
        if (width > 0 && height > 0) { mode.Fields |= 0x00080000 | 0x00100000; mode.PelsWidth = width; mode.PelsHeight = height; }
        if (hz > 0) { mode.Fields |= 0x00400000; mode.DisplayFrequency = hz; }
        var result = ChangeDisplaySettings(ref mode, 0);
        if (result != 0) throw new InvalidOperationException($"表示モードを変更できません (code {result})。");
    }

    public static void Restore(DevMode mode)
    {
        var result = ChangeDisplaySettings(ref mode, 0);
        if (result != 0) throw new InvalidOperationException($"元の表示モードへ戻せません (code {result})。");
    }
}
