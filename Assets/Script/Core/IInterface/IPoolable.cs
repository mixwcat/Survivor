/// <summary>
/// 池化对象契约。
///
/// <para>
/// <b>为什么需要它：</b>Unity 的生命周期钩子对池化对象是**不可靠**的 ——
/// 首次由 <c>Instantiate</c> 创建时对象已是激活状态，<c>SetActive(true)</c> 是空操作，
/// 因此 <c>OnEnable</c> <b>不会触发</b>；而 <c>Start</c> 只跑第一次，之后每次复用都不会再跑。
/// 计时、重新取引用、重置状态这类"每次取出都要做"的事情，必须由池显式回调，
/// 不能挂在 <c>OnEnable</c>/<c>Start</c> 上。
/// </para>
///
/// <para>
/// <b>实现约定：</b>
/// <list type="bullet">
/// <item><see cref="OnGetFromPool"/>：重置状态、重新取引用、重新计时。此时对象**已激活**。</item>
/// <item><see cref="OnReturnToPool"/>：取消 <c>Invoke</c>/协程、断开订阅、清空列表。此时对象**仍激活**。</item>
/// <item>归还必须幂等：池会用 <c>activeSelf</c> 判重，重复归还会被忽略，实现方无需自己防重。</item>
/// </list>
/// </para>
/// </summary>
public interface IPoolable
{
    /// <summary>从池中取出后调用（对象已激活）。</summary>
    void OnGetFromPool();

    /// <summary>归还到池前调用（对象仍激活）。</summary>
    void OnReturnToPool();
}
