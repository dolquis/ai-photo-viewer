namespace AiPhotoViewer.Imaging;

/// <summary>サムネイル生成とキャッシュ。引継ぎ文書 11.1 に基づきキャッシュ必須。</summary>
public interface IThumbnailGenerator
{
    /// <summary>サムネイルを生成しキャッシュパスを返す。既存キャッシュがあれば再利用する。</summary>
    Task<string> GetOrCreateThumbnailAsync(string imagePath, int maxEdgePixels, CancellationToken ct = default);
}

/// <summary>知覚ハッシュ（近似重複検知用）。</summary>
public interface IPerceptualHasher
{
    /// <summary>pHash を計算する。</summary>
    Task<string> ComputePerceptualHashAsync(string imagePath, CancellationToken ct = default);

    /// <summary>2つの知覚ハッシュ間のハミング距離を返す。小さいほど類似。</summary>
    int HammingDistance(string hashA, string hashB);
}

/// <summary>完全一致重複検知用のファイル内容ハッシュ。</summary>
public interface IFileHasher
{
    Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct = default);
}
