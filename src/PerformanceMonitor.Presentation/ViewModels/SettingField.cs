using CommunityToolkit.Mvvm.ComponentModel;

namespace PerformanceMonitor.Presentation.ViewModels;

/// <summary>設定欄位的輸入方式。</summary>
public enum SettingKind
{
    /// <summary>勾選框。</summary>
    Boolean,

    /// <summary>文字框（整數、小數、目錄）。</summary>
    Text,

    /// <summary>文字框，可空白（空白表示不使用）。</summary>
    OptionalText,

    /// <summary>多個名稱，以逗號或換行分隔。</summary>
    List,

    /// <summary>下拉選單。</summary>
    Choice,
}

/// <summary>設定頁的一個欄位。值一律以文字或勾選保存，按「儲存」時才轉換與驗證，輸入錯誤不會遺失。</summary>
/// <param name="key">settings.json 的欄位路徑，例如 <c>monitor.sampleSeconds</c>（驗證訊息以此對應）。</param>
/// <param name="label">中文名稱。</param>
/// <param name="hint">範圍與說明。</param>
/// <param name="kind">輸入方式。</param>
public sealed class SettingField(string key, string label, string hint, SettingKind kind) : ObservableObject
{
    private string _text = string.Empty;
    private bool _checked;

    /// <summary>settings.json 欄位路徑。</summary>
    public string Key { get; } = key;

    /// <summary>中文名稱。</summary>
    public string Label { get; } = label;

    /// <summary>範圍與說明（含欄位路徑）。</summary>
    public string Hint { get; } = string.IsNullOrEmpty(hint) ? key : $"{hint}（{key}）";

    /// <summary>輸入方式。</summary>
    public SettingKind Kind { get; } = kind;

    /// <summary>下拉選單的選項。</summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>文字值。</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value ?? string.Empty);
    }

    /// <summary>勾選值。</summary>
    public bool Checked
    {
        get => _checked;
        set => SetProperty(ref _checked, value);
    }

    /// <summary>是否為勾選框（畫面依此選擇輸入元件）。</summary>
    public bool IsBoolean => Kind == SettingKind.Boolean;
}

/// <summary>設定頁的一個區塊。</summary>
/// <param name="Title">區塊名稱。</param>
/// <param name="Fields">欄位。</param>
public sealed record SettingSection(string Title, IReadOnlyList<SettingField> Fields);
