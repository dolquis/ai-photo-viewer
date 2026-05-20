namespace AiPhotoViewer.Core.Domain;

/// <summary>
/// 画像埋め込みベクトル。引継ぎ文書 8.2 ImageEmbeddings に対応。
/// モデル名・バージョンを保持し、モデル変更時の再解析を可能にする。
/// </summary>
public sealed record ImageEmbedding
{
    public long Id { get; init; }
    public long ImageId { get; init; }
    public required string ModelName { get; init; }
    public required string ModelVersion { get; init; }
    public int Dimension { get; init; }

    /// <summary>正規化済み埋め込みベクトル。</summary>
    public required float[] Vector { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
