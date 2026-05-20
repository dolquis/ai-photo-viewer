namespace AiPhotoViewer.Core.Domain;

/// <summary>
/// ライブラリ内の1枚の画像ファイルを表すドメインモデル。
/// 引継ぎ文書 8.1 Images テーブルに対応する。
/// </summary>
public sealed record ImageItem
{
    public long Id { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Extension { get; init; }
    public long FileSize { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public DateTimeOffset ImportedAt { get; init; }

    /// <summary>完全一致判定用のファイル内容ハッシュ。</summary>
    public string? FileHash { get; init; }

    /// <summary>近似重複判定用の知覚ハッシュ（pHash 等）。</summary>
    public string? PerceptualHash { get; init; }

    public string? ThumbnailPath { get; init; }
    public string? ExifJson { get; init; }

    /// <summary>元ファイルが見つからない（移動/削除済み）状態か。</summary>
    public bool IsMissing { get; init; }

    public AnalysisStatus AnalysisStatus { get; init; } = AnalysisStatus.NotAnalyzed;
}
