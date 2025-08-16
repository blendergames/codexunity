using UnityEngine;
using UnityEngine.Video;

[DisallowMultipleComponent]
[AddComponentMenu("Video/Video In Scene (Quad)")]
public class VideoInScene : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Optional: assign a VideoClip from Assets.")]
    public VideoClip videoClip;

    [Tooltip("File name in StreamingAssets (e.g., myvideo.mp4) or a full http(s) URL.")]
    public string fileNameOrUrl;

    [Tooltip("If true and not using http(s), will load from StreamingAssets.")]
    public bool useStreamingAssets = true;

    [Header("Playback")]
    public bool playOnStart = true;
    public bool loop = true;
    [Range(0f, 1f)] public float volume = 1f;
    public bool mute = false;

    [Header("Quad Settings")]
    [Tooltip("Height in world units; width scales by aspect.")]
    public float quadHeight = 2f;
    public bool faceMainCamera = true;

    [Header("Feather Settings")]
    [Tooltip("Enable soft transparent edges on the video quad.")]
    public bool useFeather = true;
    [Range(0f, 0.5f)] public float featherX = 0.05f;
    [Range(0f, 0.5f)] public float featherY = 0.05f;
    [Range(0.1f, 5f)] public float featherPower = 1f;
    [Range(0f, 1f)] public float globalAlpha = 1f;

    private VideoPlayer videoPlayer;
    private AudioSource audioSource;
    private RenderTexture renderTexture;
    private GameObject quad;
    private Material quadMaterial;

    void Awake()
    {
        EnsureQuad();
        EnsureVideoPlayer();
        EnsureAudio();
    }

    void Start()
    {
        ConfigureSource();
        videoPlayer.isLooping = loop;
        if (playOnStart)
        {
            PrepareAndPlay();
        }
    }

    void EnsureQuad()
    {
        if (quad == null)
        {
            quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Video Quad";
            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = Vector3.zero;

            var col = quad.GetComponent<Collider>();
            if (col) Destroy(col);

            var renderer = quad.GetComponent<MeshRenderer>();
            var featherShader = Shader.Find("Unlit/VideoFeather");
            var fallbackShader = Shader.Find("Unlit/Texture");
            quadMaterial = new Material(featherShader != null ? featherShader : fallbackShader);
            renderer.sharedMaterial = quadMaterial;
            UpdateFeatherProps();
        }
    }

    void EnsureVideoPlayer()
    {
        if (!videoPlayer)
        {
            videoPlayer = GetComponent<VideoPlayer>();
            if (!videoPlayer) videoPlayer = gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.aspectRatio = VideoAspectRatio.NoScaling;
            videoPlayer.skipOnDrop = true;
            videoPlayer.waitForFirstFrame = true;
        }
    }

    void EnsureAudio()
    {
        if (!audioSource)
        {
            audioSource = GetComponent<AudioSource>();
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        videoPlayer.EnableAudioTrack(0, true);
        videoPlayer.SetTargetAudioSource(0, audioSource);
        audioSource.volume = volume;
        audioSource.mute = mute;
    }

    void ConfigureSource()
    {
        if (videoClip != null)
        {
            videoPlayer.source = VideoSource.VideoClip;
            videoPlayer.clip = videoClip;
        }
        else if (!string.IsNullOrEmpty(fileNameOrUrl))
        {
            videoPlayer.source = VideoSource.Url;
            string url = fileNameOrUrl;
            if (useStreamingAssets && !fileNameOrUrl.StartsWith("http"))
            {
                var fullPath = System.IO.Path.Combine(Application.streamingAssetsPath, fileNameOrUrl);
                var uri = new System.Uri(fullPath);
                url = uri.AbsoluteUri; // ensures proper file:// prefix
            }
            videoPlayer.url = url;
        }
        else
        {
            Debug.LogWarning("[VideoInScene] No video source set. Assign a VideoClip or provide a file/URL.");
        }
    }

    void PrepareAndPlay()
    {
        videoPlayer.prepareCompleted -= OnPrepared;
        videoPlayer.prepareCompleted += OnPrepared;
        videoPlayer.Prepare();
    }

    void OnPrepared(VideoPlayer vp)
    {
        AllocateRenderTextureIfNeeded();
        SetQuadScaleToAspect();

        if (faceMainCamera && Camera.main != null)
        {
            quad.transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up);
        }

        vp.Play();
        if (!mute) audioSource.Play();
    }

    void AllocateRenderTextureIfNeeded()
    {
        int w = videoPlayer.texture != null ? videoPlayer.texture.width : (int)videoPlayer.width;
        int h = videoPlayer.texture != null ? videoPlayer.texture.height : (int)videoPlayer.height;
        if (w <= 0 || h <= 0)
        {
            w = 1920; h = 1080;
        }

        if (renderTexture == null || renderTexture.width != w || renderTexture.height != h)
        {
            if (renderTexture != null)
            {
                videoPlayer.targetTexture = null;
                renderTexture.Release();
                Destroy(renderTexture);
            }

            renderTexture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32)
            {
                name = "VideoRT"
            };
            renderTexture.Create();

            videoPlayer.targetTexture = renderTexture;

            var renderer = quad.GetComponent<MeshRenderer>();
            if (renderer)
            {
                if (quadMaterial == null)
                {
                    var featherShader = Shader.Find("Unlit/VideoFeather");
                    var fallbackShader = Shader.Find("Unlit/Texture");
                    quadMaterial = new Material(featherShader != null ? featherShader : fallbackShader);
                }
                quadMaterial.mainTexture = renderTexture;
                renderer.sharedMaterial = quadMaterial;
                UpdateFeatherProps();
            }
        }
    }

    void SetQuadScaleToAspect()
    {
        float w = videoPlayer.width > 0 ? videoPlayer.width : 16f;
        float h = videoPlayer.height > 0 ? videoPlayer.height : 9f;
        float aspect = w / Mathf.Max(1f, h);
        quad.transform.localScale = new Vector3(quadHeight * aspect, quadHeight, 1f);
    }

    public void Play()
    {
        if (!videoPlayer.isPrepared)
        {
            PrepareAndPlay();
        }
        else
        {
            if (videoPlayer.targetTexture == null) AllocateRenderTextureIfNeeded();
            videoPlayer.Play();
            if (!mute) audioSource.Play();
        }
    }

    public void Pause()
    {
        videoPlayer.Pause();
        audioSource.Pause();
    }

    public void Stop()
    {
        videoPlayer.Stop();
        audioSource.Stop();
    }

    void OnDestroy()
    {
        if (renderTexture != null)
        {
            if (videoPlayer) videoPlayer.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
        }
        if (quad != null)
        {
            Destroy(quad);
        }
    }

    void UpdateFeatherProps()
    {
        if (quadMaterial == null) return;
        if (quadMaterial.shader != null && quadMaterial.shader.name == "Unlit/VideoFeather")
        {
            quadMaterial.SetFloat("_UseFeather", useFeather ? 1f : 0f);
            quadMaterial.SetVector("_Feather", new Vector4(featherX, featherY, 0f, 0f));
            quadMaterial.SetFloat("_Power", featherPower);
            quadMaterial.SetFloat("_GlobalAlpha", globalAlpha);
        }
        else
        {
            var featherShader = Shader.Find("Unlit/VideoFeather");
            if (featherShader != null)
            {
                quadMaterial.shader = featherShader;
                quadMaterial.SetFloat("_UseFeather", useFeather ? 1f : 0f);
                quadMaterial.SetVector("_Feather", new Vector4(featherX, featherY, 0f, 0f));
                quadMaterial.SetFloat("_Power", featherPower);
                quadMaterial.SetFloat("_GlobalAlpha", globalAlpha);
            }
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (quadMaterial != null)
        {
            UpdateFeatherProps();
        }
    }
#endif
}
