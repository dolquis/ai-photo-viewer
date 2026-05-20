namespace AiPhotoViewer.Infrastructure;

/// <summary>監視フォルダ内のファイル変更検知。引継ぎ文書 4.2 の再解析トリガ。</summary>
public interface ILibraryWatcher
{
    void Watch(string folderPath);
    void Unwatch(string folderPath);
    event EventHandler<LibraryFileChange>? Changed;
}

/// <summary>ファイル変更イベントの種別。</summary>
public enum LibraryFileChangeKind
{
    Created,
    Modified,
    Deleted,
    Renamed,
}

/// <summary>ファイル変更イベント。</summary>
public sealed record LibraryFileChange(LibraryFileChangeKind Kind, string Path, string? OldPath);

/// <summary>
/// アプリ設定の読み書き。引継ぎ文書 9.6 設定画面の項目に対応。
/// 設定画面 UI が具象型へダウンキャストせず値を変更できるよう、書き込み操作を公開する。
/// </summary>
public interface IAppSettings
{
    IReadOnlyList<string> WatchedFolders { get; }
    string ModelDirectory { get; set; }
    bool BackgroundAnalysisEnabled { get; set; }
    bool PauseOnBattery { get; set; }
    bool FaceRecognitionEnabled { get; set; }
    bool OcrEnabled { get; set; }
    bool PrivacyCheckEnabled { get; set; }

    void AddWatchedFolder(string folderPath);
    void RemoveWatchedFolder(string folderPath);

    Task SaveAsync(CancellationToken ct = default);
}

/// <summary>アプリ全体のロギング抽象。ログは外部送信しない。</summary>
public interface IAppLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}
