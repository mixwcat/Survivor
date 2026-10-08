using System;
using UnityEngine;

/// <summary>
/// 玩家角色控制器（按玩家实例化）—— 持有**本局扮演的角色定义**，并回答能力问题。
///
/// <para>
/// 它是"角色"这件事在运行时的唯一落点：领域层要判断"这个玩家能不能造塔"就问它，
/// 而不是各自去查场景配置或猜按钮可见性。
/// </para>
///
/// <para>
/// 定义由场景入口在生成玩家后按 <c>RunSession.characterId</c> 注入
/// （P2 的运行时生成入口）；在那之前 <see cref="IsReady"/> 为 false，
/// 调用方应当**拒绝**需要角色的操作而不是放行。
/// </para>
/// </summary>
public class PlayerRoleController : MonoBehaviour
{
    [Header("默认角色（编辑器与单机回退）")]
    [Tooltip("场景入口尚未注入角色时使用。P2 的运行时生成入口会按 RunSession 覆盖它")]
    [SerializeField] private CharacterDefinitionSO _defaultDefinition;

    private CharacterDefinitionSO _definition;

    /// <summary>
    /// 本局角色定义。运行时注入优先；未注入时回退到 Inspector 上配的默认角色 ——
    /// 否则在 P2 的运行时生成入口落地前，玩家会看不到任何升级入口（表现为"功能消失"）。
    /// </summary>
    public CharacterDefinitionSO Definition => _definition != null ? _definition : _defaultDefinition;

    /// <summary>角色是否已就绪。未就绪时一切能力判定都为 false。</summary>
    public bool IsReady => Definition != null;

    /// <summary>角色变更（参数：新定义）。UI 订阅它刷新入口按钮。</summary>
    public event Action<CharacterDefinitionSO> RoleChanged;

    /// <summary>注入角色定义（场景入口调用，幂等：同一份定义重复注入不重复发事件）。</summary>
    public void SetDefinition(CharacterDefinitionSO definition)
    {
        if (definition == null || ReferenceEquals(_definition, definition)) return;

        _definition = definition;
        RoleChanged?.Invoke(Definition);
    }

    /// <summary>
    /// 领域层权限校验。**命令执行前必须调用它** ——
    /// 未就绪（没选角色）时一律返回 false，宁可什么都不发生，也不要放行一个无主的命令。
    /// </summary>
    public bool Has(CharacterCapability capability)
    {
        CharacterDefinitionSO definition = Definition;
        if (definition == null) return false;

        return (definition.capabilities & capability) == capability;
    }
}
