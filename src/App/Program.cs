using Avalonia;
using System;

namespace AiPhotoViewer.App;

internal sealed class Program
{
    // 初期化コード。AppMain 呼び出し前に Avalonia / サードパーティ API や
    // SynchronizationContext 依存コードを使わないこと。
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia 構成。ビジュアルデザイナからも参照されるため削除しないこと。
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
