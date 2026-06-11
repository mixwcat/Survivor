using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一实体配置 SO 基类
/// 职责：实体类型 + Prefab 引用 + 数值 SO 引用 + 升级列表引用
/// UI 显示与消耗等信息由调用方从 LevelUpSO 中获取
/// </summary>
public class BaseEntitySO : ScriptableObject
{
    [Header("实体类型")]
    public EntityType entityType;

    [Header("数值引用")]
    [Tooltip("指向独立的数值配置 SO，运行时从此加载基础属性")]
    public BaseEntityDataSO dataRef;

    [Header("升级选项")]
    public List<LevelUpSO> upgrades = new();

    [Header("升级来源标记")]
    [Tooltip("勾选后，该实体的 upgrades 会进入玩家升级随机池")]
    public bool includeInPlayerUpgradePool;
}
