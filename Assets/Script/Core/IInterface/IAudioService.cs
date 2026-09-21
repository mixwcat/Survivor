using System.Threading.Tasks;

/// <summary>
/// 音频服务（纯本地表现）。
/// 统一 BGM / 音效播放与音量控制，业务代码不再直接操作 AudioSource。
/// </summary>
public interface IAudioService
{
    /// <summary>加载音频资源（由组合根调用，幂等）</summary>
    Task InitializeAsync();

    /// <summary>播放背景音乐（循环，替换当前 BGM）</summary>
    void PlayBgm(ResourceEnum clip);

    /// <summary>播放一次性音效</summary>
    void PlaySfx(ResourceEnum clip);

    /// <summary>BGM 是否静音</summary>
    bool BgmMuted { get; set; }

    /// <summary>音效是否启用</summary>
    bool SfxEnabled { get; set; }

    /// <summary>BGM 音量 0..1</summary>
    float BgmVolume { get; set; }

    /// <summary>音效音量 0..1</summary>
    float SfxVolume { get; set; }
}
