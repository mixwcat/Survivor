using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实体数据注册表 —— 统一配置所有 Entity → DataSO 的映射
/// 解决 Entity Data 分散在多个 Prefab/Scene Inspector 中难以管理的问题
///
/// 使用方式：
///   1. 在 Project 中 Create → Game/Data/Entity Data Registry，创建 Registry asset
///   2. 在 SOManager 的 Entity Data Registry 字段中拖入该 asset
///   3. 在每个 EntityBehaviour 子类的 Inspector 上填写 Registry Id（如 "Player" / "Enemy" / "Tower_Teto"）
///   4. 移除 Inspector 上的 _entityData 拖拽引用（留空即可，由 Registry 自动注入）
/// </summary>
[CreateAssetMenu(fileName = "EntityDataRegistry", menuName = "Game/Data/Entity Data Registry")]
public class EntityDataRegistry : ScriptableObject
{
    [Header("实体 → DataSO 映射表")]
    [Tooltip("entityId 与 EntityBehaviour._registryId 对应")]
    public List<EntityDataEntry> entries = new();

    /// <summary>
    /// 根据 entityId 查找对应的 DataSO
    /// </summary>
    public BaseEntityDataSO GetData(string entityId)
    {
        if (string.IsNullOrEmpty(entityId)) return null;
        var entry = entries.Find(e => e.entityId == entityId);
        return entry?.dataSO;
    }

    /// <summary>
    /// 泛型版本，自动转换类型
    /// </summary>
    public T GetData<T>(string entityId) where T : BaseEntityDataSO
    {
        return GetData(entityId) as T;
    }
}

/// <summary>
/// 单条实体数据映射
/// </summary>
[Serializable]
public class EntityDataEntry
{
    [Tooltip("标识，对应 EntityBehaviour._registryId，如 Player / Enemy / Tower_Teto / Weapon_FireBall")]
    public string entityId;

    [Tooltip("对应的 DataSO asset")]
    public BaseEntityDataSO dataSO;
}
