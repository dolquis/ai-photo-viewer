using AiPhotoViewer.Core.Domain;
using AiPhotoViewer.Jobs;
using Xunit;

namespace AiPhotoViewer.Tests;

/// <summary>
/// 雛形のスモークテスト。各層のプロジェクト参照が解決でき、
/// ドメイン型が想定どおり構築できることを確認する。
/// 実装フェーズで機能ごとのテストに置き換わる。
/// </summary>
public class ScaffoldSmokeTests
{
    [Fact]
    public void ImageItem_DefaultsToNotAnalyzed()
    {
        var image = new ImageItem
        {
            FilePath = @"C:\photos\IMG_0001.jpg",
            FileName = "IMG_0001.jpg",
            Extension = ".jpg",
        };

        Assert.Equal(AnalysisStatus.NotAnalyzed, image.AnalysisStatus);
    }

    [Fact]
    public void AnalysisJob_RetainsKindAndPriority()
    {
        var job = new AnalysisJob(ImageId: 1, AnalysisJobKind.Embedding, JobPriority.High);

        Assert.Equal(AnalysisJobKind.Embedding, job.Kind);
        Assert.Equal(JobPriority.High, job.Priority);
    }
}
