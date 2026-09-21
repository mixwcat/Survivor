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
    protected override void Awake()
    {
        base.Awake();
    }

    private float _attackTimer;

    protected virtual void OnEnable()
    {
        Owner?.Weapons?.RegisterWeapon(this);
        _attackTimer = 0f;
    }

    protected virtual void OnDisable()
    {
        Owner?.Weapons?.UnregisterWeapon(this);
    }

    private PlayerController _owner;

    /// <summary>武器所属玩家（从父级解析，按玩家实例化）</summary>
    protected PlayerController Owner
    {
        get
        {
            if (_owner == null)
                _owner = GetComponentInParent<PlayerController>();
            return _owner;
        }
    }

    /// <summary>
    /// 武器能否运作的前置条件。子类可 override 添加额外限制（弹药、目标等）。
    /// </summary>
    protected virtual bool CanOperate()
    {
        return Owner != null;
    }

    /// <summary>
    /// 按实际攻击间隔累计计时器；返回 true 表示该进行一次攻击。
    /// 已自动处理玩家存在性、interval <= 0 等防御情况。
    /// 子类可 override GetFireInterval 改变计时基准。
    /// </summary>
    protected bool TryFire()
    {
        if (!CanOperate()) return false;

        float interval = GetFireInterval();
        if (interval <= 0f)
        {
            Debug.LogWarning($"[{nameof(BaseWeapon)}] fire interval <= 0 on {GetType().Name}");
            return false;
        }

        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            _attackTimer = interval;
            return true;
        }
        return false;
    }

    #region 通用属性（从玩家 StatModel 读取，升级同步）

    /// <summary>
    /// 实际用于 TryFire 的间隔。默认等于 AttackInterval；
    /// 子类可 override 以加入投射物存在时间等额外冷却。
    /// </summary>
    protected virtual float GetFireInterval()
    {
        return GetAttackInterval();
    }

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
        return GetStat(StatType.Damage);
    }

    #endregion
}
