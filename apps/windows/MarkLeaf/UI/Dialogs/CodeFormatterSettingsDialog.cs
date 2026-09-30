using MarkLeaf.Services;
using MarkLeaf.Services.CodeFormatting;
using MarkLeaf.Services.Settings;
using MarkLeaf.UI.Controls;

namespace MarkLeaf.UI.Dialogs;

/// <summary>
/// 外部代码格式化器管理对话框：列出工具与探测状态，支持按工具设置自定义
/// 可执行/jar 路径（立即生效），以及 sqlfluff 使用的 SQL 方言。变更通过
/// onChanged 通知宿主持久化并重新下发 setCodeFormatterSettings。
/// </summary>
internal sealed class CodeFormatterSettingsDialog : Form
{
    private readonly CodeFormatterSettings _settings;
    private readonly Action _onChanged;
    private readonly ListView _toolList = new();
    private readonly TextBox _pathTextBox = new();
    private readonly ComboBox _sqlDialectCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button _browseButton = new();
    private readonly Button _clearButton = new();
    private readonly Label _statusLabel = new();

    public CodeFormatterSettingsDialog(CodeFormatterSettings settings, Action onChanged)
    {
        _settings = settings;
        _onChanged = onChanged;

        Text = Loc.Get("codeFormatter.dialogTitle");
        BackColor = DialogColors.Secondary;
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(this.ScaleForDpi(520), this.ScaleForDpi(430));
        Padding = new Padding(this.ScaleForDpi(9));

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = Loc.Get("codeFormatter.sqlDialect"),
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0, this.ScaleForDpi(4), this.ScaleForDpi(6), 0),
        }, 0, 0);
        foreach (var dialect in SqlDialects)
        {
            _sqlDialectCombo.Items.Add(dialect);
        }

        _sqlDialectCombo.SelectedIndex = Math.Max(0, SqlDialects.IndexOf(_settings.SqlDialect));
        _sqlDialectCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_sqlDialectCombo.SelectedItem is { } selected)
            {
                _settings.SqlDialect = (string)selected;
                _onChanged();
            }
        };
        layout.Controls.Add(_sqlDialectCombo, 1, 0);

        _toolList.Dock = DockStyle.Fill;
        _toolList.View = View.Details;
        _toolList.FullRowSelect = true;
        _toolList.HideSelection = false;
        _toolList.MultiSelect = false;
        _toolList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _toolList.Columns.Add(Loc.Get("codeFormatter.column.tool"), this.ScaleForDpi(150));
        _toolList.Columns.Add(Loc.Get("codeFormatter.column.languages"), this.ScaleForDpi(210));
        _toolList.Columns.Add(Loc.Get("codeFormatter.column.status"), this.ScaleForDpi(130));
        _toolList.SelectedIndexChanged += (_, _) => OnSelectedToolChanged();
        layout.Controls.Add(_toolList, 0, 1);
        layout.SetColumnSpan(_toolList, 2);

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            AutoSize = true,
            Margin = new Padding(0, this.ScaleForDpi(6), 0, 0),
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _pathTextBox.Dock = DockStyle.Fill;
        _pathTextBox.ReadOnly = true;
        _pathTextBox.PlaceholderText = Loc.Get("codeFormatter.pathPlaceholder");
        pathRow.Controls.Add(_pathTextBox, 0, 0);
        _browseButton.Text = Loc.Get("codeFormatter.browse");
        _browseButton.AutoSize = true;
        _browseButton.Click += (_, _) => BrowseToolPath();
        pathRow.Controls.Add(_browseButton, 1, 0);
        _clearButton.Text = Loc.Get("codeFormatter.clearPath");
        _clearButton.AutoSize = true;
        _clearButton.Click += (_, _) =>
        {
            if (SelectedTool() is not { } tool)
            {
                return;
            }

            _settings.ToolPaths.Remove(tool.Id);
            _pathTextBox.Text = string.Empty;
            RefreshToolList();
            _onChanged();
        };
        pathRow.Controls.Add(_clearButton, 2, 0);
        layout.Controls.Add(pathRow, 0, 2);
        layout.SetColumnSpan(pathRow, 2);

        _statusLabel.AutoSize = true;
        _statusLabel.ForeColor = SystemColors.GrayText;
        _statusLabel.Text = Loc.Get("codeFormatter.hint");
        _statusLabel.Margin = new Padding(0, this.ScaleForDpi(6), 0, 0);
        layout.Controls.Add(_statusLabel, 0, 3);
        layout.SetColumnSpan(_statusLabel, 2);

        Controls.Add(layout);

        RefreshToolList();
    }

    private static readonly List<string> SqlDialects =
    [
        "ansi", "bigquery", "clickhouse", "duckdb", "hive", "materialize", "mysql",
        "oracle", "postgres", "redshift", "snowflake", "sparksql", "sqlite", "trino", "tsql",
    ];

    private ExternalCodeFormatterTool? SelectedTool()
    {
        return _toolList.SelectedItems.Count == 0
            ? null
            : ExternalCodeFormatterCatalog.Tools.FirstOrDefault(tool => tool.Id == (string)_toolList.SelectedItems[0].Tag!);
    }

    private void RefreshToolList()
    {
        var selectedId = SelectedTool()?.Id;
        _toolList.BeginUpdate();
        _toolList.Items.Clear();
        foreach (var tool in ExternalCodeFormatterCatalog.Tools)
        {
            var custom = _settings.ToolPaths.TryGetValue(tool.Id, out var path) && !string.IsNullOrWhiteSpace(path);
            var availability = ExternalCodeFormatterCatalog.ProbeAvailability(tool, _settings.ToolPaths);
            var status = availability switch
            {
                ExternalCodeFormatterAvailability.Available when custom => Loc.Get("codeFormatter.status.customAvailable"),
                ExternalCodeFormatterAvailability.Available => Loc.Get("codeFormatter.status.available"),
                _ => Loc.Get("codeFormatter.status.notInstalled"),
            };
            var item = new ListViewItem(tool.DisplayName)
            {
                Tag = tool.Id,
            };
            item.SubItems.Add(string.Join(", ", tool.Languages));
            item.SubItems.Add(status);
            _toolList.Items.Add(item);
            if (tool.Id == selectedId)
            {
                item.Selected = true;
            }
        }

        _toolList.EndUpdate();
    }

    private void OnSelectedToolChanged()
    {
        if (SelectedTool() is not { } tool)
        {
            _pathTextBox.Text = string.Empty;
            _browseButton.Enabled = _clearButton.Enabled = false;
            return;
        }

        _browseButton.Enabled = true;
        _clearButton.Enabled = _settings.ToolPaths.ContainsKey(tool.Id);
        _pathTextBox.Text = _settings.ToolPaths.TryGetValue(tool.Id, out var path) ? path : string.Empty;
    }

    private void BrowseToolPath()
    {
        if (SelectedTool() is not { } tool)
        {
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = Loc.Format("codeFormatter.browseTitle", tool.DisplayName),
            CheckFileExists = true,
            RestoreDirectory = true,
            Filter = tool.Id == "google-java-format"
                ? $"{tool.DisplayName} (*.jar)|*.jar|{Loc.Get("codeFormatter.filterExecutable")} (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|{Loc.Get("codeFormatter.filterAll")} (*.*)|*.*"
                : $"{Loc.Get("codeFormatter.filterExecutable")} (*.exe;*.cmd;*.bat)|*.exe;*.cmd;*.bat|{Loc.Get("codeFormatter.filterAll")} (*.*)|*.*",
        };
        if (ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings.ToolPaths[tool.Id] = dialog.FileName;
        _pathTextBox.Text = dialog.FileName;
        RefreshToolList();
        _onChanged();
    }
}
