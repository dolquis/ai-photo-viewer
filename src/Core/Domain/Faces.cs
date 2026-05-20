namespace AiPhotoViewer.Core.Domain;

/// <summary>
/// 画像内で検出された顔。引継ぎ文書 8.5 Faces に対応。
/// 顔特徴量はプライバシー情報のためローカル保存に限定する。
/// </summary>
public sealed record Face
{
    public long Id { get; init; }
    public long ImageId { get; init; }
    public int BoundingBoxX { get; init; }
    public int BoundingBoxY { get; init; }
    public int BoundingBoxWidth { get; init; }
    public int BoundingBoxHeight { get; init; }
    public double Confidence { get; init; }

    /// <summary>同一人物クラスタリング用の顔特徴量。</summary>
    public required float[] Embedding { get; init; }

    /// <summary>所属する人物クラスタ。未割り当て時は null。</summary>
    public long? PersonId { get; init; }

    public double QualityScore { get; init; }
}

/// <summary>人物クラスタ。引継ぎ文書 8.6 Persons に対応。</summary>
public sealed record Person
{
    public long Id { get; init; }
    public string? DisplayName { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public bool IsHidden { get; init; }
}
