using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 触发器 → 交互注册的**唯一**桥梁：挂在带 trigger collider 的物体上，
/// 玩家进出范围时调用 <see cref="IInteractor.Register"/> / <see cref="IInteractor.Unregister"/>。
///
/// <para>
/// <b>为什么要有它：</b>旧写法把这段逻辑抄在每个交互物里（塔、武器台各一份），
/// 每加一个交互物就多一份"trigger 自配 + 进出场 + 防重入 + 玩家解析 + 销毁兜底"的样板，
/// 而其中任何一处写漏都是静默的（表现为"靠近了没反应"）。
/// </para>
///
/// <para>
/// <b>按交互者分别记账：</b>旧 <c>DetectPlayer</c> 只存一个 <c>_interaction</c> 字段 ——
/// 两个玩家进范围时第二个覆盖第一个，OnDisable 只从最后一个注销，
/// 另一个玩家的列表里就留下了一条已销毁的条目。这里用 <see cref="HashSet{T}"/> 逐个记账。
/// </para>
///
/// <para>
/// <b>目标默认同物体：</b>留空 <see cref="TargetBehaviour"/> 时自动取同物体上实现
/// <see cref="IInteractable"/> 的组件 —— 于是"加一个交互物"不需要在 Inspector 里拖引用。
/// </para>
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class InteractionSensor : MonoBehaviour
{
    [Tooltip("可交互目标。留空 = 自动取同物体上实现 IInteractable 的组件")]
    public MonoBehaviour TargetBehaviour;

    /// <summary>已进入范围的交互者（按交互者分别记账，多人安全）。</summary>
    private readonly HashSet<IInteractor> _inside = new HashSet<IInteractor>();

    private bool _resolved;
    private IInteractable _target;

    /// <summary>本传感器负责的可交互物（解析失败为 null）。</summary>
    public IInteractable Target
    {
        get
        {
            if (!_resolved)
            {
                _resolved = true;
                _target = Resolve();
            }

            return _target;
        }
    }

    private void Reset()
    {
        // 挂上组件时自动配好触发器，避免"忘了勾 isTrigger"导致玩家被挡在交互物外面
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        if (Target == null)
        {
            Debug.LogError($"[InteractionSensor] {gameObject.name} 上没有 IInteractable 目标，" +
                           "交互永远不会触发（TargetBehaviour 是否配错类型？）。");
        }
    }

    private IInteractable Resolve()
    {
        if (TargetBehaviour != null)
        {
            IInteractable fromField = TargetBehaviour as IInteractable;
            if (fromField == null)
            {
                Debug.LogError($"[InteractionSensor] {gameObject.name} 的 TargetBehaviour " +
                               $"（{TargetBehaviour.GetType().Name}）没有实现 IInteractable。");
            }

            return fromField;
        }

        return GetComponent<IInteractable>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        IInteractable target = Target;
        if (target == null) return;

        if (!InteractorResolver.TryResolve(other, out IInteractor interactor)) return;
        if (!_inside.Add(interactor)) return;

        interactor.Register(target);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        IInteractable target = Target;
        if (target == null) return;

        if (!InteractorResolver.TryResolve(other, out IInteractor interactor)) return;
        if (!_inside.Remove(interactor)) return;

        interactor.Unregister(target);
    }

    private void OnDisable()
    {
        // 交互物被销毁/禁用时主动注销：Unity 在对方被 Destroy 时不保证补发 OnTriggerExit2D，
        // 残留在玩家候选列表里的已销毁条目会让"按 E"抛 MissingReferenceException
        IInteractable target = Target;
        if (target == null || _inside.Count == 0) return;

        foreach (IInteractor interactor in _inside)
        {
            // 接口引用上的 ?. / == null 挡不住 Unity 伪 null，必须过 IsAlive
            if (InteractorResolver.IsAlive(interactor)) interactor.Unregister(target);
        }

        _inside.Clear();
    }
}
