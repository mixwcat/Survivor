using System;
using System.Collections.Generic;

/// <summary>
/// 玩家档案 —— **持久数据**：跨对局、跨进程保留，落盘为 <c>profile.json</c>。
///
/// <para>
/// <b>只存稳定字符串 id</b>：不存 SO 引用、场景对象、枚举序号或 Addressables 地址。
/// 理由分别是 —— SO/场景对象无法可靠序列化；枚举序号会在中段增删后静默错位
/// （见 `CLAUDE.md` 的标识符规范）；地址会随资源改名失效。
/// 武器一律用 <c>weapon_gun</c> 这类 id，使用时再解析成 SO。
/// </para>
///
/// <para>
/// <b>它是纯数据</b>：不读文件、不改金币、不发事件 —— 那些属于 <c>IPlayerProfileService</c>。
/// 这样存档格式可以单独演进与迁移，不会被业务逻辑缠住。
/// </para>
/// </summary>
[Serializable]
public class PlayerProfile
{
    /// <summary>
    /// 当前档案格式版本。**字段增删后必须 +1**，并在
    /// <c>PlayerProfileService.Migrate</c> 里处理旧版本，否则老档案会带着缺失字段被当成新版读进来。
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;

    /// <summary>金币余额（悬赏奖励，用于解锁武器）。</summary>
    public int coins;

    /// <summary>已解锁的武器 id（<c>weapon_gun</c> / <c>weapon_cannon</c> …）。</summary>
    public List<string> unlockedWeaponIds = new List<string>();

    /// <summary>当前所在哨站（从 1 起）。通关一次 +1，失败不推进。</summary>
    public int currentOutpost = 1;

    public AudioSettingsData audio = new AudioSettingsData();

    public GraphicsSettingsData graphics = new GraphicsSettingsData();
}

/// <summary>音频设置（随档案落盘，由 <c>IPlayerProfileService</c> 应用到 <c>IAudioService</c>）。</summary>
[Serializable]
public class AudioSettingsData
{
    public float bgmVolume = 0.5f;
    public float sfxVolume = 0.5f;
    public bool bgmMuted;
    public bool sfxEnabled = true;
}

/// <summary>画面设置（随档案落盘）。</summary>
[Serializable]
public class GraphicsSettingsData
{
    public bool fullscreen = true;

    /// <summary><c>QualitySettings</c> 的索引；-1 表示「用平台默认」，不主动改动。</summary>
    public int qualityLevel = -1;
}
