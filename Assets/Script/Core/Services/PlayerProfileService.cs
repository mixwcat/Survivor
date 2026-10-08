using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LitJson;
using UnityEngine;

/// <summary>
/// 玩家档案服务（全局服务）—— 档案的唯一读写入口。
///
/// 与 <see cref="AssetService"/> / <see cref="AudioService"/> / <see cref="UIService"/> 遵循同一套规范：
/// 挂在 [GameBootstrap] 上，由组合根 AddComponent → 注册 → InitializeAsync；
/// 业务代码通过 <see cref="Service"/> 访问（未注册返回 null）；不暴露具体类型单例。
///
/// <para>
/// <b>为什么不用 <c>JsonUtility</c>、也不另写一层 Json 管理类：</b>序列化本身用
/// <c>LitJson.JsonMapper</c> 就够了（它支持 <c>Dictionary</c> 与嵌套结构，
/// <c>JsonUtility</c> 两者都不支持）；真正需要被"封装"的不是序列化，而是**落盘**——
/// 原子写入、备份恢复、版本迁移、损坏修复、失败可见。
/// 一个只包了 <c>ToJson</c> / <c>FromJson</c> 的通用管理器解决不了上面任何一条，
/// 却会给出第二条"怎么存数据"的路径（本项目曾有这样的 <c>JsonMgr</c>，已删除）。
/// </para>
/// </summary>
public class PlayerProfileService : MonoBehaviour, IPlayerProfileService
{
    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IPlayerProfileService Service =>
        ServiceLocator.TryGet<IPlayerProfileService>(out var svc) ? svc : null;

    private const string FileName = "profile.json";

    /// <summary>哨站编号上限：防止损坏档案里的天文数字让"下一站"永远不可达。</summary>
    private const int MaxOutpost = 99;

    /// <summary>与 <c>EntitySOValidator</c> 同一套 id 规范：小写字母开头，后跟小写字母/数字/下划线。</summary>
    private static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled);

    private PlayerProfile _profile;
    private Task _initTask;

    public PlayerProfile Profile => _profile;

    public event Action Changed;

    public Task InitializeAsync()
    {
        return _initTask ??= InitializeInternalAsync();
    }

    private Task InitializeInternalAsync()
    {
        _profile = LoadOrDefault();
        ApplyAudioSettings();
        return Task.CompletedTask;
    }

    private void OnDestroy()
    {
        if (ServiceLocator.TryGet<IPlayerProfileService>(out var svc) && ReferenceEquals(svc, this))
            ServiceLocator.Unregister<IPlayerProfileService>();
    }

    // ── 读 ──

    private static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>上一次成功保存留下的旧档案（保存中断时用它恢复）。</summary>
    private static string BackupPath => SavePath + ".bak";

    private PlayerProfile LoadOrDefault()
    {
        string path = SavePath;

        if (TryReadProfile(path, out PlayerProfile profile)) return profile;

        // 正式档案**读不出来**（不存在 / 截断 / 解析失败 / IO 异常）时先试备份。
        //
        // 旧实现只在"文件不存在"时才看备份，于是"文件存在但内容坏了"直接退回默认档 ——
        // 更糟的是紧接着的任意一次 Save() 都会用 File.Replace 把这份坏档写进 .bak，
        // 把唯一能恢复的好备份覆盖掉，进度**不可逆**丢失。
        if (TryReadProfile(BackupPath, out PlayerProfile fromBackup))
        {
            Debug.LogWarning($"[PlayerProfileService] 正式档案不可用，已从备份恢复进度：{BackupPath}");
            RestoreBackupToMain(path);
            return fromBackup;
        }

        return CreateDefault();
    }

    /// <summary>
    /// 读取并校验一个候选档案。**任何失败都只返回 false，不回退默认档** ——
    /// 调用方要靠这个区分「这份文件读不了」和「没有档案」，
    /// 否则就分不出"该去试备份"还是"该建默认档"。
    /// </summary>
    private static bool TryReadProfile(string path, out PlayerProfile profile)
    {
        profile = null;
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[PlayerProfileService] 档案为空：{path}");
                return false;
            }

            PlayerProfile loaded = JsonMapper.ToObject<PlayerProfile>(json);
            if (loaded == null)
            {
                Debug.LogWarning($"[PlayerProfileService] 档案解析为 null：{path}");
                return false;
            }

            Migrate(loaded);
            Repair(loaded);
            profile = loaded;
            return true;
        }
        catch (Exception e)
        {
            // 截断 / 字段缺失 / IO 异常都走这里：只报告，不回退默认档（由调用方决定试备份还是建默认档）
            Debug.LogError($"[PlayerProfileService] 档案读取失败（{path}）：{e.Message}");
            return false;
        }
    }

    /// <summary>
    /// 把备份**复制**回正式文件。
    ///
    /// <para>
    /// 刻意不用 <c>Move</c>：备份必须留着。恢复之后如果正式文件又被写坏
    /// （或进程在下次成功保存前退出），它是唯一的退路 ——
    /// 而 <c>Save()</c> 的 <c>File.Replace</c> 会用当时的正式文件覆盖 <c>.bak</c>。
    /// </para>
    /// </summary>
    private static void RestoreBackupToMain(string path)
    {
        try
        {
            File.Copy(BackupPath, path, overwrite: true);
        }
        catch (Exception e)
        {
            // 回写失败不影响本次游玩（内存里已经有恢复出来的档案），
            // 只是下次启动仍会走一遍恢复 —— 所以是 warning 不是 error
            Debug.LogWarning($"[PlayerProfileService] 备份回写正式文件失败（下次启动会再次从备份恢复）：{e.Message}");
        }
    }

    /// <summary>
    /// 默认档案：两把**默认武器**都已解锁。
    ///
    /// <para>
    /// 必须包含 <c>weapon_spin</c>：它是工程师的默认武器，没解锁的话工程师出门就是空手 ——
    /// 而"空手"在关卡里表现为打不出任何东西，很难归因到档案初始值。
    /// </para>
    /// </summary>
    private static PlayerProfile CreateDefault()
    {
        var profile = new PlayerProfile();
        profile.unlockedWeaponIds.Add("weapon_gun");
        profile.unlockedWeaponIds.Add("weapon_spin");
        return profile;
    }

    /// <summary>
    /// 版本迁移。字段增删时在这里按 <c>schemaVersion</c> 逐级升级 ——
    /// 现在只有版本 1，所以只是把未知版本号归位。
    /// </summary>
    private static void Migrate(PlayerProfile profile)
    {
        if (profile.schemaVersion == PlayerProfile.CurrentSchemaVersion) return;

        Debug.Log($"[PlayerProfileService] 档案版本 {profile.schemaVersion} → {PlayerProfile.CurrentSchemaVersion}");
        profile.schemaVersion = PlayerProfile.CurrentSchemaVersion;
    }

    /// <summary>
    /// 校验与修复。原则是「**能修就修，不要整份丢弃**」：
    /// 丢掉一份档案等于抹掉玩家几十局的进度，而绝大多数损坏只是某个字段越界。
    /// </summary>
    private static void Repair(PlayerProfile profile)
    {
        if (profile.coins < 0) profile.coins = 0;
        profile.currentOutpost = Mathf.Clamp(profile.currentOutpost, 1, MaxOutpost);

        if (profile.unlockedWeaponIds == null) profile.unlockedWeaponIds = new List<string>();

        // 去重 + 丢弃格式非法的 id。
        // 注意这里**不**校验"未知 id" —— 那需要一份合法武器清单（注册表），
        // 而本工程刻意不做注册表反查（见 CLAUDE.md）。未知 id 在真正解析成 SO 时被忽略即可。
        var seen = new HashSet<string>();
        for (int i = profile.unlockedWeaponIds.Count - 1; i >= 0; i--)
        {
            string id = profile.unlockedWeaponIds[i];
            if (string.IsNullOrEmpty(id) || !IdPattern.IsMatch(id) || !seen.Add(id))
                profile.unlockedWeaponIds.RemoveAt(i);
        }

        if (profile.audio == null) profile.audio = new AudioSettingsData();
        if (profile.graphics == null) profile.graphics = new GraphicsSettingsData();
        profile.audio.bgmVolume = Mathf.Clamp01(profile.audio.bgmVolume);
        profile.audio.sfxVolume = Mathf.Clamp01(profile.audio.sfxVolume);
    }

    // ── 写 ──

    /// <inheritdoc />
    /// <remarks>
    /// 原子写入：先写 <c>profile.json.tmp</c>，再替换正式文件，并把旧档案留成
    /// <c>profile.json.bak</c>。这样任何时刻中断，磁盘上**至少有一份完整档案**：
    /// <list type="bullet">
    /// <item>优先用 <see cref="File.Replace(string,string,string)"/> —— 它是平台级的原子替换；</item>
    /// <item>平台不支持（部分文件系统会抛 <see cref="IOException"/> / <see cref="PlatformNotSupportedException"/>）时，
    /// 退化为「正式文件 → 备份 → 临时文件 → 正式文件」。顺序不能反：旧实现是
    /// <c>File.Delete(正式) → File.Move(tmp, 正式)</c>，两步之间进程退出就**两份都没有了**。</item>
    /// </list>
    /// </remarks>
    public bool Save()
    {
        if (_profile == null) return false;

        string path = SavePath;
        string tmp = path + ".tmp";
        string bak = BackupPath;

        try
        {
            File.WriteAllText(tmp, JsonMapper.ToJson(_profile));

            if (!File.Exists(path))
            {
                File.Move(tmp, path);
                return true;
            }

            try
            {
                File.Replace(tmp, path, bak);
                return true;
            }
            catch (PlatformNotSupportedException)
            {
                // 落到下面的兜底
            }
            catch (IOException)
            {
                // 某些文件系统 / 共享模式下 Replace 不可用，同样走兜底
            }

            if (File.Exists(bak)) File.Delete(bak);
            File.Move(path, bak);
            File.Move(tmp, path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerProfileService] 档案保存失败：{e}");
            return false;
        }
    }

    // ── 事务 ──

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0 || _profile.coins < amount) return false;

        _profile.coins -= amount;
        Changed?.Invoke();
        return true;
    }

    public void GrantCoins(int amount)
    {
        if (amount <= 0) return;

        _profile.coins += amount;
        Changed?.Invoke();
    }

    public bool IsWeaponUnlocked(string weaponId)
    {
        return !string.IsNullOrEmpty(weaponId) && _profile.unlockedWeaponIds.Contains(weaponId);
    }

    public bool TryUnlockWeapon(string weaponId, int price)
    {
        if (string.IsNullOrEmpty(weaponId) || price < 0) return false;
        if (IsWeaponUnlocked(weaponId)) return false;
        if (_profile.coins < price) return false;

        int coinsBefore = _profile.coins;
        _profile.coins -= price;
        _profile.unlockedWeaponIds.Add(weaponId);

        // 购买是明确事务点：**先落盘再通知 UI**，并且返回值必须反映落盘结果。
        // 落盘失败要回滚：旧实现"保存失败也不回滚、照样返回 true"，
        // 于是购买界面显示成功、内存里也解锁了，重启后钱和武器一起消失 ——
        // 玩家只会认为"游戏吞了我的金币"，而且没有任何线索指向存档写入失败。
        if (!Save())
        {
            _profile.coins = coinsBefore;
            _profile.unlockedWeaponIds.Remove(weaponId);

            Debug.LogError($"[PlayerProfileService] 武器「{weaponId}」购买失败：档案保存失败，已回滚扣款与解锁。");
            return false;
        }

        Changed?.Invoke();
        return true;
    }

    public void SetCurrentOutpost(int outpost)
    {
        int clamped = Mathf.Clamp(outpost, 1, MaxOutpost);
        if (clamped == _profile.currentOutpost) return;

        _profile.currentOutpost = clamped;
        Changed?.Invoke();
    }

    public void ApplyAudioSettings()
    {
        IAudioService audio = AudioService.Service;
        if (audio == null || _profile == null) return;

        audio.BgmVolume = _profile.audio.bgmVolume;
        audio.SfxVolume = _profile.audio.sfxVolume;
        audio.BgmMuted = _profile.audio.bgmMuted;
        audio.SfxEnabled = _profile.audio.sfxEnabled;
    }
}
