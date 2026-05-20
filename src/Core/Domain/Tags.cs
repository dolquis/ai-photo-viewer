namespace AiPhotoViewer.Core.Domain;

/// <summary>タグ。引継ぎ文書 8.3 Tags に対応。階層構造を持てる。</summary>
public sealed record Tag
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public long? ParentTagId { get; init; }
}

/// <summary>
/// 画像とタグの関連。引継ぎ文書 8.4 ImageTags に対応。
/// AI自動タグ・ユーザー確定タグ・ユーザー除外タグを Source で区別する。
/// </summary>
public sealed record ImageTag
{
    public long ImageId { get; init; }
    public long TagId { get; init; }
    public TagSource Source { get; init; }

    /// <summary>AIタグの信頼度（0.0〜1.0）。ユーザータグでは null。</summary>
    public double? Confidence { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
