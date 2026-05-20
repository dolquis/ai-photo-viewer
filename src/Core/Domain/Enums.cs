namespace AiPhotoViewer.Core.Domain;

/// <summary>画像のAI解析の進行状態。</summary>
public enum AnalysisStatus
{
    NotAnalyzed,
    Queued,
    InProgress,
    Completed,
    Failed,
}

/// <summary>タグの付与元。</summary>
public enum TagSource
{
    Ai,
    User,
    Excluded,
}

/// <summary>重複グループの種別。</summary>
public enum DuplicateGroupType
{
    Exact,
    NearDuplicate,
    Similar,
    Derivative,
}
