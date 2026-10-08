using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一实体配置 SO 基类
/// 职责：稳定 id + 数值 SO 引用 + 升级列表引用（表现信息由各子类补充）
/// 本 SO 由实体自身**直接引用**（<see cref="EntityBehaviour.entityConfig"/>），
/// 不再经「枚举 → 注册表」反查，因此不存在漏配枚举值而静默指向另一实体的风险。
/// </summary>
public class BaseEntitySO : ScriptableObject
{
    [Header("稳定标识")]
    [Tooltip("存档 / 网络同步用的稳定 id（如 \"tower_teto\"）。一经发布不可更改、不可复用。")]
    public string id;

    [Header("数值引用")]
    [Tooltip("指向独立的数值配置 SO，运行时从此加载基础属性")]
    public BaseEntityDataSO dataRef;

    [Header("升级选项")]
    public List<LevelUpSO> upgrades = new();
}
