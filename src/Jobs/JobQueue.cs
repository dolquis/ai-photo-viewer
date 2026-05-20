namespace AiPhotoViewer.Jobs;

/// <summary>ジョブ優先度。前景操作（閲覧中フォルダ）を優先するために用いる。</summary>
public enum JobPriority
{
    Low,
    Normal,
    High,
}

/// <summary>解析ジョブの種別。引継ぎ文書 6.2 のパイプライン段階に対応。</summary>
public enum AnalysisJobKind
{
    Metadata,
    Thumbnail,
    FileHash,
    PerceptualHash,
    Embedding,
    Tagging,
    FaceDetection,
    FaceEmbedding,
    Ocr,
    QualityAssessment,
}

/// <summary>1件の解析ジョブ。</summary>
public sealed record AnalysisJob(long ImageId, AnalysisJobKind Kind, JobPriority Priority);

/// <summary>
/// バックグラウンド解析ジョブのキュー。引継ぎ文書 6.3 に対応。
/// UI をブロックせず、優先度制御・キャンセル・一時停止/再開・進捗通知を備える。
/// </summary>
public interface IJobQueue
{
    void Enqueue(AnalysisJob job);
    void Pause();
    void Resume();

    /// <summary>キュー全体の処理をキャンセルする。</summary>
    void CancelAll();

    /// <summary>処理待ち件数。</summary>
    int PendingCount { get; }

    /// <summary>進捗（処理中ファイル・完了件数など）の通知。</summary>
    event EventHandler<JobProgress>? ProgressChanged;
}

/// <summary>ジョブ進捗の通知ペイロード。</summary>
public sealed record JobProgress(int Completed, int Total, AnalysisJob? Current);
