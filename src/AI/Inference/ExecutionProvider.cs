namespace AiPhotoViewer.AI.Inference;

/// <summary>
/// AI推論の実行プロバイダ。引継ぎ文書 7.3 の方針に基づき、
/// ONNX Runtime の実行プロバイダを差し替え可能にするための抽象。
/// </summary>
public enum ExecutionProvider
{
    /// <summary>常時利用可能なフォールバック。</summary>
    Cpu,

    /// <summary>Windows の GPU 汎用アクセラレーション（Intel/AMD/NVIDIA）。</summary>
    DirectMl,

    /// <summary>対応 NPU での実行。</summary>
    Npu,
}

/// <summary>
/// 解析結果に紐づけるモデルの識別情報。
/// 引継ぎ文書 16章「解析結果には必ずモデル名・バージョンを保存する」に対応。
/// </summary>
public sealed record ModelDescriptor(string Name, string Version);
