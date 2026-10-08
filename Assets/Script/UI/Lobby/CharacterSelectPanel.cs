using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选角面板 —— 大厅里选本局扮演谁，结果写进 <see cref="RunSession"/>。
///
/// <para>
/// 角色列表由 <see cref="LobbyDirector"/> 通过 <c>ShowPanelAsync</c> 注入，
/// 不配在 prefab 上：prefab 是资产，把"这一局有哪些角色"配进资产会让
/// 场景与资产各持一份清单，改一处漏一处。
/// </para>
///
/// <para>
/// 只写 <c>characterId</c>（稳定字符串 id），不写 SO 引用 —— 与存档同样的理由：
/// 引用无法跨网络同步，也会随资产改名失效。关卡侧由 <c>PlayerSpawner</c> 按 id 查定义。
/// </para>
/// </summary>
public class CharacterSelectPanel : BasePanel
{
    public Image img1;
    public Image img2;
    public TextMeshProUGUI txt1;
    public TextMeshProUGUI txt2;
    public Button btn1;
    public Button btn2;

    private IReadOnlyList<CharacterDefinitionSO> _characters;

    /// <summary>注入可选角色（必须在 Init 之前调用，面板据此绑定槽位）。</summary>
    public void SetCharacters(IReadOnlyList<CharacterDefinitionSO> characters)
    {
        _characters = characters;
    }

    public override void Init()
    {
        Bind(0, img1, txt1, btn1);
        Bind(1, img2, txt2, btn2);
    }

    private void Bind(int index, Image icon, TextMeshProUGUI label, Button button)
    {
        CharacterDefinitionSO definition =
            _characters != null && index >= 0 && index < _characters.Count ? _characters[index] : null;

        // 角色不足时把整个槽位藏起来，而不是留一个点了没反应的图标
        bool visible = definition != null;
        if (button != null) button.gameObject.SetActive(visible);
        if (icon != null) icon.gameObject.SetActive(visible);
        if (label != null) label.gameObject.SetActive(visible);

        if (!visible) return;

        if (icon != null) icon.sprite = definition.displaySprite;
        if (label != null) label.text = definition.displayName;
        if (button != null) button.onClick.AddListener(() => Choose(definition));
    }

    private void Choose(CharacterDefinitionSO definition)
    {
        IRunSessionService session = RunSessionService.Service;
        if (session == null || definition == null) return;

        // 写入走受控方法：它自己检查锁状态，并**原子化规范装备** ——
        // 切到工程师会把不合法的武器换成固定火球，切回枪手会清掉角色不允许的装备。
        // 这一步不能省：装备与角色不匹配只在进关卡后才表现为"这角色带着别人的武器"。
        if (!session.TrySetCharacter(definition, out string reason))
        {
            Debug.LogWarning($"[CharacterSelectPanel] 选择角色「{definition.displayName}」被拒绝：{reason}");
            return;
        }

        UIService.Service?.HidePanel<CharacterSelectPanel>();
        AudioService.Service?.PlaySfx(ResourceEnum.OnMouseClickUI);
    }
}
