using AiPhotoViewer.Core.Domain;

namespace AiPhotoViewer.Search;

/// <summary>
/// 近似最近傍ベクトルインデックス。引継ぎ文書 4.4 / 11.3 に対応。
/// HNSW 系インメモリインデックスを想定し、永続化は SQLite BLOB と併用する。
/// </summary>
public interface IVectorIndex
{
    void Add(long imageId, float[] vector);
    void Remove(long imageId);

    /// <summary>クエリベクトルに最も近い上位 k 件を類似度付きで返す。</summary>
    IReadOnlyList<VectorMatch> Search(float[] query, int k);

    int Count { get; }
}

/// <summary>ベクトル検索のヒット1件。</summary>
public sealed record VectorMatch(long ImageId, double Similarity);

/// <summary>自然言語検索・類似画像検索のファサード。</summary>
public interface ISearchService
{
    /// <summary>自然言語クエリで画像を検索する。</summary>
    Task<IReadOnlyList<SearchHit>> SearchByTextAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>指定画像に類似する画像を検索する。</summary>
    Task<IReadOnlyList<SearchHit>> SearchSimilarAsync(long imageId, int limit, CancellationToken ct = default);
}

/// <summary>検索結果のヒット1件（画像と類似度スコア）。</summary>
public sealed record SearchHit(ImageItem Image, double Score);
