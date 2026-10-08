using UnityEngine;

/// <summary>
/// 攻击实体的生成入口 —— 攻击方式（SO）通过它创建投射物/召唤物，**不再自己 <c>Instantiate</c>**。
///
/// <para>
/// <b>为什么要有这一层：</b>「谁生成、谁回收」会随运行环境改变 —— 单机走对象池
/// （<see cref="ProjectilePool"/>），联机则要走 <c>NetworkServer.Spawn</c> 并由 Mirror 管销毁。
/// 把这套差异收进一个接口，攻击方式资产与四个策略类就不必知道它。
/// </para>
///
/// <para>
/// 实现方目前是 <see cref="AttackDriver"/>（单机）；联机时由驱动层替换为网络实现，
/// 策略类与资产一行不用改。
/// </para>
/// </summary>
public interface IAttackSpawner
{
    /// <summary>
    /// 生成一个攻击实体。
    /// <list type="bullet">
    /// <item><paramref name="parent"/> 为空 = **独立飞行**的投射物 → 走对象池（高频、短命）；</item>
    /// <item><paramref name="parent"/> 非空 = **跟随载体**的召唤物（如挂在发射点下的环绕物）→
    /// 随载体一起销毁，不池化。</item>
    /// </list>
    /// </summary>
    /// <returns>生成的实例；prefab 为空或创建失败时返回 null。</returns>
    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);

    /// <summary>回收一个由本 spawner 生成的实体（池归还或销毁）。</summary>
    void Despawn(GameObject instance);
}
