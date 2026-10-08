using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 一把**已装配的武器实例**：配置 + 实例 + prefab 句柄，三者成对出现。
///
/// <para>
/// <b>为什么需要它：</b>装配这件事有三样东西必须一起记账 ——
/// 用的是哪份配置（<see cref="WeaponEntitySO"/>）、装出来的实例（<see cref="BaseWeapon"/>）、
/// 以及要归还的 Addressables 句柄。以前玩家侧与塔侧各写一份并行的 List 来记这三样，
/// 两边的失败分支（加载失败 / prefab 缺 BaseWeapon / await 期间宿主销毁）各写一遍，
/// 漏一条就是句柄泄漏。现在两边共用 <see cref="WeaponAssembler"/> 与本类型。
/// </para>
///
/// <para>
/// <b>升级状态不在这里</b>：它在 <see cref="BaseWeapon"/> 上（<see cref="IUpgradeStateHolder"/>），
/// 因为升级的落点是 <c>StatModel</c>，而 <c>StatModel</c> 属于武器实例那个 MonoBehaviour。
/// 本类型只负责"这把武器是怎么被装出来的、怎么还回去"。
/// </para>
/// </summary>
public sealed class WeaponInstance
{
    /// <summary>装配时使用的武器配置。**它是武器数值与升级项的唯一来源**。</summary>
    public WeaponEntitySO Config { get; }

    /// <summary>装出来的武器实例；装配失败时为 null（失败不会产生 WeaponInstance）。</summary>
    public BaseWeapon Weapon { get; }

    /// <summary>武器 prefab 的句柄，由装配方持有并负责归还。</summary>
    public AsyncOperationHandle<GameObject> PrefabHandle { get; }

    public WeaponInstance(WeaponEntitySO config, BaseWeapon weapon, AsyncOperationHandle<GameObject> prefabHandle)
    {
        Config = config;
        Weapon = weapon;
        PrefabHandle = prefabHandle;
    }
}
