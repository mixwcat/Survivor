/// <summary>
/// Addressables 地址常量集中定义。
/// 所有资源加载方统一从此处取地址，避免路径字符串散落各处难以维护。
/// 地址与 Editor 批处理脚本 Assets/Editor/AddressablesSetup.cs 生成的地址一一对应。
/// </summary>
public static class AssetKeys
{
    // ---- UI ----
    public const string Canvas = "UI/Canvas";
    public const string DamageNumText = "UI/DamageNumText";

    /// <summary>面板资源地址：UI/&lt;面板类名&gt;</summary>
    public static string Panel(string panelName) => $"UI/{panelName}";
    public static string Panel<T>() => Panel(typeof(T).Name);

    // ---- Common ----
    public const string ExpSprite = "Common/ExpSprite";
    public const string SpriteToHandle = "Common/SpriteToHandle";

    // ---- Weapon ----
    // 注：Common/TetoBullet、Weapon/Bullet、Weapon/Spin 三个地址**没有**对应常量 ——
    // 投射物 prefab 由 AttackDriver 用 AssetReferenceGameObject（GUID）引用，不经过地址字符串。
    // ⚠️ 但那三条 Addressables 条目不能删：AssetReference 要求目标必须是 Addressable。

    // ---- Music / Sfx ----
    /// <summary>音频资源地址：Music/&lt;音频枚举名&gt;</summary>
    public static string Music(string clipName) => $"Music/{clipName}";
}
