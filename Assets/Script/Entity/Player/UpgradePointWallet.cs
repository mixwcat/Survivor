using System;
using UnityEngine;

/// <summary>
/// 升级点钱包（按玩家实例化）。
///
/// <para>
/// 职责只有一件事：**记账**。它不认识塔、武器或升级面板 ——
/// 「花多少点买什么」由调用方决定（<c>TowerManagementPanel</c> / <c>WeaponUpgradePanel</c>），
/// 这样同一套钱包能被两种角色共用，而角色能力差异体现在"能打开哪个面板"上。
/// </para>
///
/// <para>
/// <b>退款不是幂等的</b>：<see cref="Refund"/> 调两次就会退两次。
/// 需要幂等的地方（异步放置取消、拆除）由**塔的投入账本**记录"是否已退"，
/// 而不是在这里猜 —— 钱包无法区分"第二次退款"和"另一笔合法退款"。
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class UpgradePointWallet : MonoBehaviour, IUpgradePointWallet
{
    [Header("初始点数")]
    [Tooltip("开局默认升级点。写在这里而不是硬编码：它是开局体验的一部分，需要能在 Inspector 里调")]
    [SerializeField] private int _startingPoints = 10;

    private int _balance;

    public int Balance => _balance;

    public event Action<int> Changed;
    public event Action Insufficient;

    private void Awake()
    {
        // 直接写字段而不是走 Grant：Awake 阶段还没有订阅者，
        // 发事件只会让"开局就有 10 点"这条日志/回调路径白跑一趟
        _balance = Mathf.Max(0, _startingPoints);
    }

    public bool TrySpend(int amount)
    {
        // 0 花费视为成功：免费项（如首次建造）不该因为"余额为 0"而被拒绝
        if (amount <= 0) return true;

        if (_balance < amount)
        {
            Insufficient?.Invoke();
            return false;
        }

        _balance -= amount;
        Changed?.Invoke(_balance);
        return true;
    }

    public void Grant(int amount)
    {
        if (amount <= 0) return;

        _balance += amount;
        Changed?.Invoke(_balance);
    }

    public void Refund(int amount)
    {
        Grant(amount);
    }
}
