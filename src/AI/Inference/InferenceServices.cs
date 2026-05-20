using AiPhotoViewer.Core.Domain;

namespace AiPhotoViewer.AI.Inference;

// 引継ぎ文書 10.3「推論抽象化」に対応するサービスインターフェース群。
// UI と推論実装を密結合させないため、すべてインターフェース経由で利用する。
// 各実装はモデル差し替え・実行デバイス変更が可能であること。

/// <summary>画像埋め込み（自然言語検索・類似検索の基盤）。</summary>
public interface IImageEmbeddingService
{
    ModelDescriptor Model { get; }
    Task<float[]> EmbedImageAsync(string imagePath, CancellationToken ct = default);
    Task<float[]> EmbedTextAsync(string text, CancellationToken ct = default);
}

/// <summary>画像内容の自動タグ付け。</summary>
public interface ITaggingService
{
    ModelDescriptor Model { get; }
    Task<IReadOnlyList<TagPrediction>> PredictTagsAsync(string imagePath, CancellationToken ct = default);
}

/// <summary>顔検出。</summary>
public interface IFaceDetectionService
{
    ModelDescriptor Model { get; }
    Task<IReadOnlyList<FaceDetection>> DetectFacesAsync(string imagePath, CancellationToken ct = default);
}

/// <summary>顔特徴量抽出（同一人物クラスタリング用）。</summary>
public interface IFaceRecognitionService
{
    ModelDescriptor Model { get; }
    Task<float[]> ExtractEmbeddingAsync(string imagePath, FaceDetection face, CancellationToken ct = default);
}

/// <summary>OCR（MVP後フェーズ）。</summary>
public interface IOcrService
{
    ModelDescriptor Model { get; }
    Task<IReadOnlyList<OcrResult>> RecognizeAsync(string imagePath, CancellationToken ct = default);
}

/// <summary>画質診断（MVP後フェーズ）。</summary>
public interface IQualityAssessmentService
{
    ModelDescriptor Model { get; }
    Task<QualityScore> AssessAsync(string imagePath, CancellationToken ct = default);
}

/// <summary>アップスケール（非破壊。MVP後フェーズ）。</summary>
public interface IUpscaleService
{
    ModelDescriptor Model { get; }
    Task<string> UpscaleAsync(string imagePath, string outputPath, CancellationToken ct = default);
}

/// <summary>ノイズ除去（非破壊。MVP後フェーズ）。</summary>
public interface IDenoiseService
{
    ModelDescriptor Model { get; }
    Task<string> DenoiseAsync(string imagePath, string outputPath, CancellationToken ct = default);
}

/// <summary>タグ推定結果（タグ名と信頼度）。</summary>
public sealed record TagPrediction(string Name, double Confidence);

/// <summary>顔検出結果（バウンディングボックスと信頼度）。</summary>
public sealed record FaceDetection(int X, int Y, int Width, int Height, double Confidence);
