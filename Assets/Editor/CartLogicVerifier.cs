#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 纯逻辑校验器 —— 把「无法用肉眼确认」的规则变成可批处理运行的断言。
///
/// <para>
/// <b>为什么不是 NUnit EditMode 测试：</b>本工程没有任何 asmdef，全部游戏脚本都在预定义程序集
/// <c>Assembly-CSharp</c> 里；而 asmdef 化的测试程序集**无法引用预定义程序集**
/// （Unity 的硬限制），要加 EditMode 测试就得先把 <c>Assets/Script</c> 拆成 asmdef ——
/// 那会连带要求给第三方源码目录（Joystick Pack 的 Scripts 与 Scripts/Editor）也补 asmdef，
/// 属于为了测试基础设施去改第三方资产。本工程既有的自动校验（<c>EntitySOValidator</c>、
/// <c>SoFieldAudit</c>、<c>AddressablesSetup</c>）走的就是「CLI 校验器 + 退出码」这条路，
/// 这里延续同一套：断言失败即 <c>Exit(1)</c>，可挂进 CI。
/// </para>
///
/// <para>
/// 手动运行：Tools ▸ Verify Cart Logic
/// 批处理运行：<c>-executeMethod CartLogicVerifier.VerifyFromCommandLine</c>
/// </para>
///
/// <para>
/// <b>覆盖范围：</b>奖励计算、出行会话状态迁移、塔放置事务的一次性、档案原子写入与备份恢复、
/// 哨站难度系数。**不覆盖**需要 Play 模式的运行时行为（死亡幂等、Boss 注入、升级选择队列、
/// 结算一次性）—— 那些在 `Docs/CartPlan.md` 与 `Docs/CartSystemReview.md` 的手动验证清单里。
/// </para>
/// </summary>
public static class CartLogicVerifier
{
    private static int _passed;
    private static readonly List<string> Failures = new List<string>();

    [MenuItem("Tools/Verify Cart Logic")]
    public static void VerifyFromMenu()
    {
        bool ok = RunAll();
        Debug.Log(ok ? "[CartLogicVerifier] CLI_OK (menu)" : "[CartLogicVerifier] CLI_FAIL (menu)");
    }

    public static void VerifyFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = RunAll();
        }
        catch (Exception e)
        {
            Debug.LogError($"[CartLogicVerifier] 异常: {e}");
        }
        finally
        {
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool RunAll()
    {
        _passed = 0;
        Failures.Clear();

        VerifyRewardCalculator();
        VerifyRunSessionTransitions();
        VerifyTowerPlacementTransaction();
        VerifyOutpostDifficulty();
        VerifyPauseOwnership();
        VerifyDuplicateManagerSingleton();
        VerifyProfileRoundTrip();

        if (Failures.Count == 0)
        {
            Debug.Log($"[CartLogicVerifier] 全部通过：{_passed} 项断言。");
            return true;
        }

        Debug.LogError($"[CartLogicVerifier] 失败 {Failures.Count} 项 / 通过 {_passed} 项：");
        for (int i = 0; i < Failures.Count; i++)
            Debug.LogError($"  ✗ {Failures[i]}");

        return false;
    }

    // ── 断言 ──

    private static void Check(bool condition, string what)
    {
        if (condition) _passed++;
        else Failures.Add(what);
    }

    private static void CheckEqual<T>(T actual, T expected, string what)
    {
        if (EqualityComparer<T>.Default.Equals(actual, expected)) _passed++;
        else Failures.Add($"{what}（期望 {expected}，实际 {actual}）");
    }

    // ── 奖励计算 ──

    private static void VerifyRewardCalculator()
    {
        CheckEqual(RewardCalculator.Calculate(RunOutcome.Defeat, 999, 1f),
                   RewardCalculator.DefeatReward, "失败奖励固定，且与击杀/耐久无关");

        CheckEqual(RewardCalculator.Calculate(RunOutcome.Victory, 0, 0f),
                   RewardCalculator.MinVictoryReward, "胜利下限：0 杀 0 耐久也拿 100");

        CheckEqual(RewardCalculator.Calculate(RunOutcome.Victory, 100000, 1f),
                   RewardCalculator.MaxVictoryReward, "胜利上限：刷击杀不能突破 500");

        // 纯函数：同样的输入必须得到同样的输出（不许读时间/随机）
        int a = RewardCalculator.Calculate(RunOutcome.Victory, 37, 0.42f);
        int b = RewardCalculator.Calculate(RunOutcome.Victory, 37, 0.42f);
        CheckEqual(a, b, "同一个输入两次计算结果一致（纯函数）");

        // 耐久被 Clamp01：>1 与 1 等价，<0 与 0 等价
        CheckEqual(RewardCalculator.Calculate(RunOutcome.Victory, 10, 5f),
                   RewardCalculator.Calculate(RunOutcome.Victory, 10, 1f), "耐久 > 1 被夹到 1");
        CheckEqual(RewardCalculator.Calculate(RunOutcome.Victory, 10, -3f),
                   RewardCalculator.Calculate(RunOutcome.Victory, 10, 0f), "耐久 < 0 被夹到 0");

        // 击杀数为负按 0 处理（损坏的统计不该让奖励变负）
        CheckEqual(RewardCalculator.Calculate(RunOutcome.Victory, -5, 0f),
                   RewardCalculator.Calculate(RunOutcome.Victory, 0, 0f), "负数击杀按 0 处理");
    }

    // ── 出行会话 ──

    private static void VerifyRunSessionTransitions()
    {
        // 枪手：上限 2、默认带两把（枪 + 炮）；工程师：上限 1、默认固定火球
        CharacterDefinitionSO gunner = CreateCharacter("char_gunner", 2,
            new[] { "weapon_gun", "weapon_cannon" }, "weapon_gun", "weapon_cannon", "weapon_laser");
        CharacterDefinitionSO engineer = CreateCharacter("char_engineer", 1,
            new[] { "weapon_spin" }, "weapon_spin");

        try
        {
            var session = new RunSession();
            session.BeginNew(1234);

            CheckEqual(session.Snapshot().Seed, 1234, "BeginNew 写入种子");
            Check(!session.IsReadyToDepart(out _), "什么都没配时不能出发");

            // 选角色 → 自动补默认武器（补到携带上限：枪手两把）
            Check(session.TrySetCharacter(gunner, out string reason), $"设置枪手被接受（{reason}）");
            CheckEqual(session.Snapshot().WeaponLoadoutIds.Count, 2, "选角色后补满默认武器（枪手两把）");
            CheckEqual(session.Snapshot().WeaponLoadoutIds[0], "weapon_gun", "第一把是默认列表的第一项");
            CheckEqual(session.Snapshot().WeaponLoadoutIds[1], "weapon_cannon", "第二把补到携带上限");

            // 关卡没配好仍然不能出发 —— 出发判据是"角色 + 关卡 + 装备"三项齐全
            Check(!session.IsReadyToDepart(out _), "只有角色、没有关卡时不能出发");

            Check(session.TrySetStage("stage_01", out _), "设置关卡被接受");
            Check(session.IsReadyToDepart(out _), "角色 + 关卡 + 装备齐全后可以出发");

            // 换角色要**原子化规范装备**：工程师不能带枪
            Check(session.TrySetCharacter(engineer, out _), "切换到工程师被接受");
            CheckEqual(session.Snapshot().WeaponLoadoutIds.Count, 1, "切角色后装备被规范成 1 把");
            CheckEqual(session.Snapshot().WeaponLoadoutIds[0], "weapon_spin", "工程师被换成固定 Spin");

            // 白名单拒绝
            Check(!session.TrySetLoadout(new[] { "weapon_gun" }, out _), "工程师不能携带白名单外的武器");

            // 上限拒绝
            Check(!session.TrySetLoadout(new[] { "weapon_spin", "weapon_spin" }, out _), "重复 id 被拒绝");
            CheckEqual(session.Snapshot().WeaponLoadoutIds.Count, 1, "被拒绝的写入没有改动任何状态");

            // 空列表拒绝（不能空手出门）
            Check(!session.TrySetLoadout(Array.Empty<string>(), out _), "空装备列表被拒绝");

            // 锁定 → 一切写入被拒
            Check(session.TryLockForDeparture(out string lockReason), $"锁定成功（{lockReason}）");
            Check(session.IsLocked, "锁定后 IsLocked 为 true");
            Check(!session.TrySetCharacter(gunner, out _), "锁定后不能改角色");
            Check(!session.TrySetStage("stage_02", out _), "锁定后不能改关卡");
            Check(!session.TrySetLoadout(new[] { "weapon_spin" }, out _), "锁定后不能改装备");
            CheckEqual(session.Snapshot().CharacterId, "char_engineer", "锁定后的写入没有改动角色");
            CheckEqual(session.Snapshot().StageId, "stage_01", "锁定后的写入没有改动关卡");

            // 取消出发（解锁）后又能改
            session.Unlock();
            Check(!session.IsLocked, "Unlock 后 IsLocked 为 false");
            Check(session.TrySetCharacter(gunner, out _), "解锁后可以重新选角色");

            // 配置不全时不允许锁定 —— 锁住一份不能出发的配置会让玩家连补救机会都没有
            var fresh = new RunSession();
            fresh.BeginNew(1);
            Check(!fresh.TryLockForDeparture(out _), "配置不全时拒绝锁定");
            Check(!fresh.IsLocked, "被拒绝的锁定没有把状态改成已锁");

            // 快照是只读视图：拿到的列表不能改回 session
            RunSessionSnapshot snap = session.Snapshot();
            Check(snap.WeaponLoadoutIds is not List<string>, "快照里的装备列表不是可变 List");

            // Clear 清空
            session.Clear();
            CheckEqual(session.Snapshot().CharacterId, null, "Clear 清空角色");
            CheckEqual(session.Snapshot().WeaponLoadoutIds.Count, 0, "Clear 清空装备");
            CheckEqual(session.Snapshot().IsLocked, false, "Clear 复位锁定状态");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gunner);
            UnityEngine.Object.DestroyImmediate(engineer);
        }
    }

    private static CharacterDefinitionSO CreateCharacter(string id, int maxSlots,
                                                         string[] defaultWeapons, params string[] allowed)
    {
        var so = ScriptableObject.CreateInstance<CharacterDefinitionSO>();
        so.id = id;
        so.defaultWeaponIds = new List<string>(defaultWeapons);
        so.maxWeaponSlots = maxSlots;
        so.allowedWeaponIds = new List<string>(allowed);
        return so;
    }

    // ── 塔放置事务 ──

    private static void VerifyTowerPlacementTransaction()
    {
        // Payer 传 null：这里只校验**状态机**的一次性语义；
        // 退款是否真的落到钱包由 Play 验证清单覆盖（需要活着的玩家对象）
        var tx = new TowerPlacementTransaction(null, 3);

        CheckEqual(tx.State, TowerPlacementTransaction.TransactionState.Loading, "事务初始为 Loading");
        Check(!tx.IsFinalized, "Loading 不算终结");
        Check(tx.TryMarkReady(), "Loading → Ready");
        CheckEqual(tx.State, TowerPlacementTransaction.TransactionState.Ready, "状态迁移到 Ready");

        // 同帧双击：第二次提交必须失败
        Check(tx.TryCommit(), "第一次提交成功");
        Check(!tx.TryCommit(), "第二次提交被拒绝（同帧双击只生成一座塔）");
        Check(tx.IsFinalized, "提交后事务已终结");

        // 提交之后不能再取消 —— 否则确认过的塔会被"退一次款"
        Check(!tx.TryCancel(), "已提交的事务不能再取消");
        CheckEqual(tx.State, TowerPlacementTransaction.TransactionState.Committed, "状态仍是 Committed");

        // 取消路径：一次性
        var cancelled = new TowerPlacementTransaction(null, 2);
        cancelled.TryMarkReady();
        Check(cancelled.TryCancel(), "第一次取消成功");
        Check(!cancelled.TryCancel(), "第二次取消是空操作（不会重复退款）");
        Check(!cancelled.TryCommit(), "已取消的事务不能再提交");
        CheckEqual(cancelled.State, TowerPlacementTransaction.TransactionState.Cancelled, "状态是 Cancelled");

        // 加载失败路径：还没 Ready 也能取消并退款
        var failed = new TowerPlacementTransaction(null, 5);
        Check(failed.TryCancel(), "Loading 阶段失败也能取消（加载失败要退款）");
        Check(!failed.TryMarkReady(), "已取消的事务不能再变成 Ready");

        // 负数花费被夹到 0：退款金额不该是负数
        var negative = new TowerPlacementTransaction(null, -7);
        CheckEqual(negative.Cost, 0, "负数花费被夹到 0");
    }

    // ── 哨站难度 ──

    private static void VerifyOutpostDifficulty()
    {
        // 没有档案服务时退化为哨站 1 = 不加成（不是"随机数"也不是"报错"）
        CheckEqual(OutpostDifficulty.Multiplier, 1f, "没有档案时系数为 1");

        // 公式本身：哨站 2 = +15%，且被上限夹住
        float expectedOutpost2 = 1f + OutpostDifficulty.BonusPerOutpost;
        Check(Mathf.Abs(expectedOutpost2 - 1.15f) < 0.0001f, "哨站 2 的系数是 1.15");

        Check(OutpostDifficulty.MaxBonus > 0f, "上限为正数（否则系数会被夹成 0 以下）");

        // 注入是幂等的：同一个模型连续注入两次，数值不能叠加
        var model = new EntityStatModel();
        model.SetBaseValue(StatType.MaxHealth, 100f);

        OutpostDifficulty.Apply(model);
        float afterFirst = model.GetStat(StatType.MaxHealth);
        OutpostDifficulty.Apply(model);
        float afterSecond = model.GetStat(StatType.MaxHealth);

        CheckEqual(afterSecond, afterFirst, "重复注入不会累计（池化复用的关键约束）");
    }

    // ── 暂停所有权 ──

    /// <summary>
    /// 暂停是**有所有权**的：每个持有者拿自己的令牌申请一次、只释放自己那一次，
    /// 集合空了才恢复时间。布尔式暂停下"两个模态面板重叠，先关的那个恢复全局时间"
    /// 是必现的。
    /// </summary>
    private static void VerifyPauseOwnership()
    {
        float originalTimeScale = Time.timeScale;

        var host = new GameObject("[CartLogicVerifier]Level");
        try
        {
            var level = host.AddComponent<GameLevelManager>();

            object tokenA = new object();
            object tokenB = new object();

            level.AcquirePause(tokenA);
            CheckEqual(Time.timeScale, 0f, "第一个持有者申请后时间冻结");
            Check(!level.IsGameActive, "暂停时 IsGameActive 为 false");

            // 第二个模态面板重叠
            level.AcquirePause(tokenB);
            level.ReleasePause(tokenA);
            CheckEqual(Time.timeScale, 0f, "还有持有者时**不会**恢复时间（重叠面板的关键约束）");
            Check(!level.IsGameActive, "还有持有者时仍算暂停");

            // 幂等：重复申请/重复释放都不改变计数
            level.AcquirePause(tokenB);
            level.ReleasePause(tokenA);
            CheckEqual(Time.timeScale, 0f, "重复申请与重复释放都是空操作");

            // 非持有者释放不该误恢复别人的暂停
            level.ReleasePause(new object());
            CheckEqual(Time.timeScale, 0f, "非持有者释放不会误恢复");

            level.ReleasePause(tokenB);
            CheckEqual(Time.timeScale, 1f, "最后一个持有者释放后才恢复时间");
            Check(level.IsGameActive, "恢复后 IsGameActive 为 true");

            // 空令牌被忽略（不能因为一个 null 就把计数搞乱）
            level.AcquirePause(null);
            CheckEqual(Time.timeScale, 1f, "null 令牌不会造成暂停");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            Time.timeScale = originalTimeScale;
        }
    }

    // ── 重复 Manager ──

    /// <summary>
    /// 场景里出现两个同类 Manager 时：重复实例**不能**成为主实例（因此不该注册服务），
    /// 而它的销毁也**不能**把主实例的服务注销掉。
    ///
    /// <para>
    /// 这里直接断言两处纯逻辑（<c>ManagerSingleton.TryClaimInstance</c> 与
    /// <c>ServiceLocator.UnregisterIfSelf</c>），而不是去 AddComponent 等 Awake ——
    /// **编辑模式下 MonoBehaviour 的 Awake 不会执行**（除非标了 ExecuteAlways），
    /// 靠 AddComponent 观察注册行为只会得到一个永远"通过"的假象。
    /// </para>
    /// </summary>
    private static void VerifyDuplicateManagerSingleton()
    {
        var hostA = new GameObject("[CartLogicVerifier]ManagerA");
        var hostB = new GameObject("[CartLogicVerifier]ManagerB");

        try
        {
            var primary = hostA.AddComponent<RunStatsTracker>();
            var duplicate = hostB.AddComponent<RunStatsTracker>();

            // ① 归属判定：第一个取得主实例身份，第二个被判定为重复
            Check(RunStatsTracker.TryClaimInstance(primary, out bool primaryDuplicate),
                  "第一个实例取得主实例身份");
            Check(!primaryDuplicate, "第一个实例不是重复实例");

            Check(!RunStatsTracker.TryClaimInstance(duplicate, out bool duplicateFlag),
                  "第二个实例拿不到主实例身份");
            Check(duplicateFlag, "第二个实例被判定为重复实例（因此不会注册服务）");

            Check(RunStatsTracker.TryClaimInstance(primary, out _), "同一实例重复判定仍然成功（幂等）");

            // ② 注销语义：非注册者不能把主实例的服务带走
            ServiceLocator.Register(primary);
            Check(!ServiceLocator.UnregisterIfSelf<RunStatsTracker>(duplicate), "非注册者注销被拒绝");
            Check(ReferenceEquals(RunStatsTracker.Service, primary), "重复实例销毁后服务仍指向主实例");

            Check(ServiceLocator.UnregisterIfSelf<RunStatsTracker>(primary), "注册者自己注销成功");
            Check(RunStatsTracker.Service == null, "主实例注销后服务为空");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(hostA);
            UnityEngine.Object.DestroyImmediate(hostB);
        }
    }

    // ── 档案原子写入与备份恢复 ──

    private static void VerifyProfileRoundTrip()
    {
        string path = Path.Combine(Application.persistentDataPath, "profile.json");
        string bak = path + ".bak";
        string tmp = path + ".tmp";

        // 备份现场，测完还原 —— 校验器不该破坏开发者本机的存档
        byte[] originalMain = File.Exists(path) ? File.ReadAllBytes(path) : null;
        byte[] originalBak = File.Exists(bak) ? File.ReadAllBytes(bak) : null;

        try
        {
            DeleteQuietly(path);
            DeleteQuietly(bak);
            DeleteQuietly(tmp);

            // ① 全新档案 + 保存
            PlayerProfileService service = CreateProfileService();
            Check(service.Profile != null, "首次启动有默认档案（不会是 null）");
            Check(service.IsWeaponUnlocked("weapon_gun"), "默认档案里 gun 已解锁");
            Check(service.IsWeaponUnlocked("weapon_spin"), "默认档案里 spin 已解锁（工程师的默认武器）");

            service.GrantCoins(1000);
            service.SetCurrentOutpost(3);
            Check(service.Save(), "保存成功");
            Check(File.Exists(path), "保存后正式档案存在");
            Check(!File.Exists(tmp), "保存后没有残留临时文件");

            // 回归护栏：public const / static 字段**不能**被写进存档。
            // 它们写不回去（const 抛 "Cannot set a constant field"），一旦被导出，
            // 读档就会在反序列化时整体失败并静默退回默认档 —— 存档看着完好，进度却全丢。
            string raw = File.ReadAllText(path);
            Check(raw.Contains("\"coins\":1000"), "存档 JSON 里确实写入了金币（不是空对象）");
            Check(!raw.Contains("CurrentSchemaVersion"), "静态/常量字段没有被写进存档");

            // ② 重新加载：数据要能读回来
            PlayerProfileService reloaded = CreateProfileService();
            CheckEqual(reloaded.Profile.coins, 1000, "金币被正确读回");
            CheckEqual(reloaded.Profile.currentOutpost, 3, "哨站被正确读回");

            // ③ 正式档案损坏 + **没有**备份 → 回退默认档而不是崩溃
            DeleteQuietly(bak);
            File.WriteAllText(path, "{ 这不是 JSON");
            PlayerProfileService broken = CreateProfileService();
            Check(broken.Profile != null, "档案损坏且无备份时回退默认档而不是抛异常");

            // ④ 正式档案缺失 + 备份存在 → 从备份恢复（保存中断的那条路径）
            DeleteQuietly(path);
            File.WriteAllText(bak, "{\"schemaVersion\":1,\"coins\":777,\"unlockedWeaponIds\":[\"weapon_gun\"],\"currentOutpost\":2}");
            PlayerProfileService recovered = CreateProfileService();
            CheckEqual(recovered.Profile.coins, 777, "正式档案缺失时从备份恢复");
            Check(File.Exists(path), "恢复后正式档案回来了");
            Check(File.Exists(bak), "恢复后备份仍然保留（它是唯一退路，不能被消费掉）");

            // ⑤ 购买事务：成功后余额与解锁都要落盘
            DeleteQuietly(path);
            DeleteQuietly(bak);
            PlayerProfileService buyer = CreateProfileService();
            buyer.GrantCoins(500);
            Check(buyer.TryUnlockWeapon("weapon_cannon", 300), "余额充足时购买成功");
            CheckEqual(buyer.Profile.coins, 200, "购买后扣款正确");
            Check(buyer.IsWeaponUnlocked("weapon_cannon"), "购买后武器已解锁");

            Check(!buyer.TryUnlockWeapon("weapon_cannon", 300), "重复购买被拒绝");
            Check(!buyer.TryUnlockWeapon("weapon_laser", 9999), "余额不足时购买被拒绝");
            CheckEqual(buyer.Profile.coins, 200, "失败的购买没有扣款");

            PlayerProfileService persisted = CreateProfileService();
            Check(persisted.IsWeaponUnlocked("weapon_cannon"), "购买结果已落盘（重启后仍解锁）");

            // ⑥ 正式档案**损坏** + 备份有效 → 必须恢复备份，而且不能把备份吃掉。
            // 这是数据安全的关键路径：旧实现只在"文件不存在"时才看备份，
            // "文件存在但坏了"直接退回默认档，紧接着的 Save() 还会把好备份覆盖成坏档。
            DeleteQuietly(path);
            DeleteQuietly(bak);

            PlayerProfileService seeded = CreateProfileService();
            seeded.GrantCoins(888);
            seeded.SetCurrentOutpost(4);
            Check(seeded.Save(), "建立一份可恢复的档案");
            Check(!File.Exists(bak), "首次保存没有备份（正式文件本来就是新建的）");

            // 再存一次：File.Replace 会把上一份正式文件写进 .bak
            seeded.GrantCoins(1);
            Check(seeded.Save(), "第二次保存留下备份");
            Check(File.Exists(bak), "第二次保存产生了 .bak");

            // 把正式档案写坏
            File.WriteAllText(path, "{ 截断的 JSON");

            PlayerProfileService fromCorruptBackup = CreateProfileService();
            CheckEqual(fromCorruptBackup.Profile.coins, 888,
                       "正式档案损坏时从备份恢复（而不是退回默认档）");
            CheckEqual(fromCorruptBackup.Profile.currentOutpost, 4, "恢复出来的哨站进度正确");
            Check(File.Exists(bak), "恢复后备份仍然保留（否则下一次保存会把它覆盖成坏档）");
            Check(File.Exists(path), "恢复后正式文件被重新写好");
        }
        finally
        {
            if (_profileHost != null) UnityEngine.Object.DestroyImmediate(_profileHost);
            _profileHost = null;

            // 还原现场
            Restore(path, originalMain);
            Restore(bak, originalBak);
            DeleteQuietly(tmp);
        }
    }

    /// <summary>
    /// 建一个档案服务并等它初始化完。上一个实例会被销毁（服务自身会在 OnDestroy 里注销）。
    /// </summary>
    private static PlayerProfileService CreateProfileService()
    {
        if (_profileHost != null) UnityEngine.Object.DestroyImmediate(_profileHost);

        _profileHost = new GameObject("[CartLogicVerifier]");
        var service = _profileHost.AddComponent<PlayerProfileService>();

        Task init = service.InitializeAsync();
        // InitializeAsync 内部是同步完成的（读文件 + 建默认档），这里只做一次兜底等待
        if (!init.IsCompleted) init.Wait(TimeSpan.FromSeconds(5));

        return service;
    }

    private static GameObject _profileHost;

    private static void DeleteQuietly(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception e) { Debug.LogWarning($"[CartLogicVerifier] 删除 {path} 失败：{e.Message}"); }
    }

    private static void Restore(string path, byte[] content)
    {
        try
        {
            if (content == null) { if (File.Exists(path)) File.Delete(path); }
            else File.WriteAllBytes(path, content);
        }
        catch (Exception e) { Debug.LogWarning($"[CartLogicVerifier] 还原 {path} 失败：{e.Message}"); }
    }
}
#endif
