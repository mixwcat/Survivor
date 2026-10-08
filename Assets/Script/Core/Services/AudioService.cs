using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 音频服务：统一 BGM / 音效播放与音量控制。
/// 使用 AudioSource 对象池 + PlayOneShot，替代原先「每个音效 new GameObject 再 Destroy」的实现。
/// 挂载于 [GameBootstrap] 物体，由组合根创建并注册为 IAudioService。
/// </summary>
public class AudioService : MonoBehaviour, IAudioService
{
    private const int SfxPoolSize = 8;

    /// <summary>兼容层：优先 ServiceLocator；未注册时返回 null</summary>
    public static IAudioService Service =>
        ServiceLocator.TryGet<IAudioService>(out var svc) ? svc : null;

    private readonly Dictionary<ResourceEnum, AudioClip> _clips = new Dictionary<ResourceEnum, AudioClip>();
    private readonly List<AsyncOperationHandle<AudioClip>> _handles = new List<AsyncOperationHandle<AudioClip>>();
    private readonly List<AudioSource> _sfxPool = new List<AudioSource>();

    private AudioSource _bgm;
    private int _sfxIndex;
    private Task _initTask;

    private float _bgmVolume = 0.5f;
    private float _sfxVolume = 0.5f;
    private bool _bgmMuted;
    private bool _sfxEnabled = true;

    public bool BgmMuted
    {
        get => _bgmMuted;
        set { _bgmMuted = value; if (_bgm != null) _bgm.mute = value; }
    }

    public bool SfxEnabled
    {
        get => _sfxEnabled;
        set => _sfxEnabled = value;
    }

    public float BgmVolume
    {
        get => _bgmVolume;
        set { _bgmVolume = value; if (_bgm != null) _bgm.volume = value; }
    }

    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = value;
    }

    private void Awake()
    {
        _bgm = gameObject.AddComponent<AudioSource>();
        _bgm.loop = true;
        _bgm.playOnAwake = false;
        _bgm.volume = _bgmVolume;
        _bgm.mute = _bgmMuted;

        for (int i = 0; i < SfxPoolSize; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            _sfxPool.Add(src);
        }
    }

    /// <summary>加载音频资源（由组合根调用，幂等 + 并发安全）。</summary>
    public Task InitializeAsync()
    {
        return _initTask ??= InitializeInternalAsync();
    }

    private async Task InitializeInternalAsync()
    {
        IAssetService assetService = AssetService.Service;
        if (assetService == null)
        {
            Debug.LogError("[AudioService] IAssetService 未注册，音频无法加载。");
            return;
        }

        foreach (ResourceEnum res in System.Enum.GetValues(typeof(ResourceEnum)))
        {
            string address = AssetKeys.Music(res.ToString());
            AsyncOperationHandle<AudioClip> handle = default;

            try
            {
                handle = assetService.LoadAssetAsync<AudioClip>(address);
                AudioClip clip = await handle.Task;

                if (clip == null)
                {
                    Debug.LogWarning($"[AudioService] 音频资源缺失：{address}");
                    // 失败路径也必须归还句柄，否则引用计数只增不减、bundle 永不卸载
                    if (handle.IsValid()) handle.Release();
                    continue;
                }

                _clips[res] = clip;
                _handles.Add(handle);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AudioService] 加载失败 {address}: {e.Message}");
                if (handle.IsValid()) handle.Release();
            }
        }

        PlayBgm(ResourceEnum.bgm);
    }

    public void PlayBgm(ResourceEnum clip)
    {
        if (_bgm == null) return;
        if (!_clips.TryGetValue(clip, out AudioClip audioClip) || audioClip == null)
        {
            Debug.LogWarning($"[AudioService] 未加载的 BGM：{clip}");
            return;
        }

        if (_bgm.clip == audioClip && _bgm.isPlaying) return;

        _bgm.clip = audioClip;
        _bgm.volume = _bgmVolume;
        _bgm.mute = _bgmMuted;
        _bgm.Play();
    }

    public void PlaySfx(ResourceEnum clip)
    {
        if (!_sfxEnabled || _sfxPool.Count == 0) return;
        if (!_clips.TryGetValue(clip, out AudioClip audioClip) || audioClip == null)
        {
            Debug.LogWarning($"[AudioService] 未加载的音效：{clip}");
            return;
        }

        AudioSource src = _sfxPool[_sfxIndex];
        _sfxIndex = (_sfxIndex + 1) % _sfxPool.Count;
        src.PlayOneShot(audioClip, _sfxVolume);
    }

    private void OnDestroy()
    {
        if (ServiceLocator.TryGet<IAudioService>(out var svc) && ReferenceEquals(svc, this))
            ServiceLocator.Unregister<IAudioService>();

        foreach (AsyncOperationHandle<AudioClip> handle in _handles)
        {
            if (handle.IsValid())
                handle.Release();
        }
        _handles.Clear();
        _clips.Clear();
    }
}
