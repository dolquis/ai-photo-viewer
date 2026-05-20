namespace AiPhotoViewer.UI.ViewModels;

/// <summary>
/// メイン画面の ViewModel スタブ。
/// 実装フェーズ（Phase 1）でフォルダツリー・サムネイルグリッド・検索バー・
/// AI解析進捗の各サブ ViewModel を保持するよう拡張する。
/// </summary>
public sealed class MainWindowViewModel : ViewModelBase
{
    private string _searchQuery = string.Empty;

    /// <summary>自然言語検索バーの入力テキスト。</summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetField(ref _searchQuery, value);
    }

    /// <summary>ウィンドウタイトル等に用いる表示名。</summary>
    public string Greeting => "AI Photo Viewer";
}
