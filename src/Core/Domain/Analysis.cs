namespace AiPhotoViewer.Core.Domain;

/// <summary>OCR認識結果。引継ぎ文書 8.7 OcrResults に対応。</summary>
public sealed record OcrResult
{
    public long Id { get; init; }
    public long ImageId { get; init; }
    public required string Text { get; init; }
    public string? Language { get; init; }
    public double Confidence { get; init; }

    /// <summary>テキスト領域の座標（JSON）。</summary>
    public string? BoundingBoxJson { get; init; }

    /// <summary>生成に用いたモデル名。モデル更新時の再解析対象特定に用いる。</summary>
    public required string ModelName { get; init; }

    /// <summary>生成に用いたモデルのバージョン。</summary>
    public required string ModelVersion { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>画質診断スコア。引継ぎ文書 8.8 QualityScores に対応。</summary>
public sealed record QualityScore
{
    public long ImageId { get; init; }

    /// <summary>生成に用いたモデル名。モデル更新時の再解析対象特定に用いる。</summary>
    public required string ModelName { get; init; }

    /// <summary>生成に用いたモデルのバージョン。</summary>
    public required string ModelVersion { get; init; }

    public double BlurScore { get; init; }
    public double NoiseScore { get; init; }
    public double ExposureScore { get; init; }
    public double SharpnessScore { get; init; }
    public double FaceQualityScore { get; init; }
    public double OverallScore { get; init; }

    /// <summary>「ブレあり」「白飛び」等のラベル一覧（JSON）。</summary>
    public string? LabelsJson { get; init; }
}
