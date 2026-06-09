using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器基类
/// 继承 EntityBehaviour，持有 WeaponDataSO 来初始化 StatModel
/// 通用属性（攻击速度、基础伤害）从玩家 StatModel 读取，实现升级同步
/// 专属属性从武器自己的 StatModel 读取
/// </summary>
public class BaseWeapon : EntityBehaviour
{
    [Header("武器标签")]
    [Tooltip("用于 SOManager 升级池过滤，如 FireBall / Gun / Ranged / Melee")]
    public List<string> weaponTags = new List<string>();

    protected override void Awake()
    {
        base.Awake();
    }

    protected virtual void OnEnable()
    {
        WeaponManager.Instance.RegisterWeapon(this);
    }

    protected virtual void OnDisable()
    {
        WeaponManager.Instance.UnregisterWeapon(this);
    }

    #region 通用属性（从玩家 StatModel 读取，升级同步）

    /// <summary>
    /// 攻击间隔 — 读取自己的StateModel
    /// </summary>
    protected float GetAttackInterval()
    {
        return GetStat(StatType.AttackInterval);
    }

    /// <summary>
    /// 基础伤害 — 读取自己的StatModel
    /// </summary>
    protected float GetBaseDamage()
    {
        return GetStat(StatType.BaseDamage);
    }

    #endregion
}
