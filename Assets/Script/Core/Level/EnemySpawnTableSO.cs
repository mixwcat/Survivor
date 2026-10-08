using System;
using UnityEngine;

/// <summary>生成表的频率模型。</summary>
public enum SpawnMode
{
    /// <summary>按速率持续生成（只/秒）。</summary>
    Rate = 0,

    /// <summary>按波次生成（每波 N 只 + 波间隔）。</summary>
    Wave = 1,
}

/// <summary>生成方向（相对锚点 = 推车）。</summary>
public enum SpawnSide
{
    /// <summary>前后随机（各 50%）。</summary>
    Both = 0,

    /// <summary>只从前方堵截。</summary>
    Front = 1,

    /// <summary>只从后方追击。</summary>
    Back = 2,
}

/// <summary>一种敌人的权重项。</summary>
[Serializable]
public struct SpawnEntry
{
    [Tooltip("敌人配置。prefab 为空会被启动期体检点名")]
    public EnemyEntitySO Entity;

    [Tooltip("相对权重（0 = 这一行不出它）。所有项都是 0 时该行不可用")]
    public float Weight;
}

/// <summary>
/// 一条生成规则的**载荷** —— "多快 / 多少 / 什么 / 从哪"。
///
/// <para>
/// <b>没有"何时"字段</b>：何时生效由它所在的容器决定 ——
/// 放在 <see cref="SpawnSegment.Rules"/> 里就是"车行驶在这一段时"，
/// 放在 <see cref="SpawnNode.Rules"/> 里就是"车停在这个节点期间"。
/// 旧版本用"压力档 + 弧长区间"两个过滤字段表达这件事，等于让每行自己声明一次身份，
/// 既啰嗦又容易填错（区间不勾开关就是死字段）。
/// </para>
///
/// <para>
/// 本结构是**纯数据**，运行时状态（计时器、已生成计数、实例列表）在
/// <see cref="EnemySpawner"/> 里按行维护 —— SO 上不放任何运行时状态。
/// </para>
/// </summary>
[Serializable]
public struct SpawnRule
{
    [Header("频率")]
    public SpawnMode Mode;

    [Tooltip("Rate 模式：每秒生成几只")]
    public float Rate;

    [Tooltip("Wave 模式：每波几只")]
    public int WaveCount;

    [Tooltip("Wave 模式：波间隔（秒）。进入生效范围的**第一波立刻出**")]
    public float WaveInterval;

    [Header("总量与上限")]
    [Tooltip("本行**总共**生成多少只，达到后本行停止（0 = 不限，随所在段/节点结束而停）。" +
             "⚠️ 节点规则必须填 > 0 —— 否则车会永远停在这个节点")]
    public int TotalCount;

    [Tooltip("本行同时存在的上限（0 = 不限）。到上限时本行暂停生成，计时也暂停")]
    public int MaxAlive;

    [Header("什么（按权重随机）")]
    public SpawnEntry[] Entries;

    [Header("从哪来（相对锚点 = 推车）")]
    public SpawnSide Side;

    [Tooltip("生成点与锚点的横向距离（0 = 生成在推车身上）")]
    public float DistanceX;

    [Tooltip("生成点与锚点的纵向随机范围（±这个值）")]
    public float RangeY;
}

/// <summary>
/// 一段路（相邻两个折点之间）的刷怪方式。**骨架由烘焙生成**（标签与米数），内容由作者填。
/// </summary>
[Serializable]
public struct SpawnSegment
{
    [Tooltip("烘焙生成的标签（「P0 → P1（起点 → 充能点 1）」）。改它没有副作用，但重新烘焙会覆盖")]
    public string Label;

    [Tooltip("烘焙生成的起点弧长（米）。运行时读它判断车是否在这一段")]
    public float StartDistance;

    [Tooltip("烘焙生成的终点弧长（米）")]
    public float EndDistance;

    [Tooltip("车行驶在这一段时生效的规则。规则之间互相独立、同时生效（生成量叠加）")]
    public SpawnRule[] Rules;
}

/// <summary>
/// 一个折点的刷怪方式。**只有会停车的折点**（充能点 / 终点）才有"停留期间"这个窗口。
/// </summary>
[Serializable]
public struct SpawnNode
{
    [Tooltip("烘焙生成的标签（「P1 · 充能点 1」）")]
    public string Label;

    [Tooltip("烘焙生成：节点类型。None = 普通折点（不停车，规则永远不会生效，体检会红错）")]
    public CartNodeKind Kind;

    [Tooltip("烘焙生成：该节点所在的弧长（米）")]
    public float Distance;

    [Tooltip("车停在该节点期间生效的规则。**这些规则必须有限（TotalCount > 0）**，" +
             "因为放行条件是「全部刷完且全部清掉」")]
    public SpawnRule[] Rules;
}

/// <summary>
/// 敌人**生成表** —— 一条路径一份，**骨架由 <c>CartRouteBaker</c> 从路径烘焙生成**。
///
/// <para>
/// <b>结构 = 路径的结构：</b>段（相邻折点之间）+ 节点（折点）。作者因此不需要手填
/// 弧长数字，只要回答"这一段怎么刷""停在这个点怎么刷"。路径改了重新烘焙，
/// 骨架跟着更新，**已填的规则内容按下标保留**。
/// </para>
///
/// <para>
/// <b>Boss 就是节点上的一条规则</b>：以前"到点刷 Boss"是一条硬编码的链
/// （<c>StageDirector</c> 阶段 → <c>BossEncounter</c> → 订阅死亡 → 恢复行驶），
/// 现在它只是"节点规则里的一条 Wave（TotalCount = 1）"。放行条件统一为
/// **该节点的怪全部刷完且全部清掉**（见 <see cref="EnemySpawner.NodeCleared"/>）。
/// </para>
///
/// <para>
/// <b>停摆没有专门的档位</b>：段规则的生效条件只看"弧长在不在这段"（车停不停都一样），
/// 节点规则只看"停没停在这个节点"。停摆期间因此照常按当前段/节点的规则刷怪。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "SpawnTable", menuName = "Game/Level/Spawn Table")]
public class EnemySpawnTableSO : ScriptableObject
{
    [Tooltip("与路径的相邻折点对一一对应（由烘焙生成）")]
    public SpawnSegment[] Segments = Array.Empty<SpawnSegment>();

    [Tooltip("与路径的折点一一对应（由烘焙生成）")]
    public SpawnNode[] Nodes = Array.Empty<SpawnNode>();
}
