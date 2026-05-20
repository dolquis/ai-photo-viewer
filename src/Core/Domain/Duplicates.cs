namespace AiPhotoViewer.Core.Domain;

/// <summary>重複/類似/派生画像のグループ。引継ぎ文書 8.9 DuplicateGroups に対応。</summary>
public sealed record DuplicateGroup
{
    public long Id { get; init; }
    public DuplicateGroupType GroupType { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>重複グループの構成要素。引継ぎ文書 8.10 DuplicateGroupItems に対応。</summary>
public sealed record DuplicateGroupItem
{
    public long GroupId { get; init; }
    public long ImageId { get; init; }

    /// <summary>グループ代表との類似度（0.0〜1.0）。</summary>
    public double SimilarityScore { get; init; }

    /// <summary>AIが保持を推奨する画像か。</summary>
    public bool RecommendedKeep { get; init; }

    /// <summary>推奨理由（解像度が高い、無圧縮、等）。</summary>
    public string? Reason { get; init; }
}
