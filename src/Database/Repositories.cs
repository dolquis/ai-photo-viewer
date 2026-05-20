using AiPhotoViewer.Core.Domain;

namespace AiPhotoViewer.Database;

// SQLite を永続化先とするリポジトリ抽象。引継ぎ文書 7.4 / 8章のデータモデルに対応。
// 実装フェーズで Microsoft.Data.Sqlite ベースの実装を追加する。

/// <summary>画像メタデータの永続化。</summary>
public interface IImageRepository
{
    Task<long> AddAsync(ImageItem image, CancellationToken ct = default);
    Task<ImageItem?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ImageItem?> GetByPathAsync(string filePath, CancellationToken ct = default);
    Task UpdateAsync(ImageItem image, CancellationToken ct = default);
    Task<IReadOnlyList<ImageItem>> ListByFolderAsync(string folderPath, CancellationToken ct = default);
}

/// <summary>埋め込みベクトルの永続化。</summary>
public interface IEmbeddingRepository
{
    Task SaveAsync(ImageEmbedding embedding, CancellationToken ct = default);
    Task<ImageEmbedding?> GetByImageIdAsync(long imageId, string modelName, CancellationToken ct = default);
}

/// <summary>データベース初期化（スキーマ作成・マイグレーション）。</summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken ct = default);
}
