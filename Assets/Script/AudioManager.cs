using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("Audio Mixer")]
    public AudioMixer mainMixer;

    [Header("Sumber Suara")]
    public AudioSource sumberMusic;
    public AudioSource sumberSFX;

    [Header("Koleksi Lagu BGM")]
    public AudioClip bgmMenu;
    public AudioClip bgmProlog;
    public AudioClip bgmInGame;
    public AudioClip bgmTegang;
    public AudioClip bgmGoodEnding; // Tambahan baru
    public AudioClip bgmBadEnding;  // Tambahan baru

    [Header("Koleksi SFX")]
    public AudioClip sfxTombol;

    [Header("Pengaturan Fade")]
    public float durasiFade = 1.2f;
    private Coroutine prosesFade;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void PastikanAudioManagerAda()
    {
        if (instance == null)
        {
            AudioManager prefab = Resources.Load<AudioManager>("AudioManager");
            if (prefab != null)
            {
                Instantiate(prefab);
            }
        }
    }
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += SaatSceneBerubah;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= SaatSceneBerubah;
    }

    void Start()
    {
        float volMusic = PlayerPrefs.GetFloat("SimpanMusic", 1f);
        float volSFX = PlayerPrefs.GetFloat("SimpanSFX", 1f);

        AturVolumeMusic(volMusic);
        AturVolumeSFX(volSFX);
    }

   void SaatSceneBerubah(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainInGame")
        {
            GantiBGM(bgmInGame);
        }
        else if (scene.name == "Prolog")
        {
            GantiBGM(bgmProlog);
        }
        else if (scene.name == "GoodEnding")
        {
            GantiBGM(bgmGoodEnding);
        }
        else if (scene.name == "BadEnding")
        {
            GantiBGM(bgmBadEnding);
        }
        else
        {
            GantiBGM(bgmMenu);
        }
    }

    public void GantiBGM(AudioClip laguBaru)
    {
        if (laguBaru == null) return;
        if (sumberMusic.clip == laguBaru && sumberMusic.isPlaying) return;

        if (prosesFade != null) StopCoroutine(prosesFade);
        prosesFade = StartCoroutine(ProsesFadeBGM(laguBaru));
    }

    IEnumerator ProsesFadeBGM(AudioClip laguBaru)
    {
        // Fade Out lagu lama
        if (sumberMusic.isPlaying)
        {
            float waktu = 0f;
            float volAwal = sumberMusic.volume;
            while (waktu < durasiFade / 2f)
            {
                waktu += Time.deltaTime;
                sumberMusic.volume = Mathf.Lerp(volAwal, 0f, waktu / (durasiFade / 2f));
                yield return null;
            }
        }

        sumberMusic.Stop();
        sumberMusic.clip = laguBaru;
        sumberMusic.volume = 0f;
        sumberMusic.Play();

        // Fade In lagu baru
        float waktuIn = 0f;
        while (waktuIn < durasiFade / 2f)
        {
            waktuIn += Time.deltaTime;
            sumberMusic.volume = Mathf.Lerp(0f, 1f, waktuIn / (durasiFade / 2f));
            yield return null;
        }
        sumberMusic.volume = 1f;
    }

    public void MulaiMusikTegang()
    {
        GantiBGM(bgmTegang);
    }

    public void KembaliMusikSantai()
    {
        GantiBGM(bgmInGame);
    }

    public void PutarSFXTombol()
    {
        if (sfxTombol != null)
            sumberSFX.PlayOneShot(sfxTombol);
    }

    public void PutarSFX(AudioClip klipKustom)
    {
        if (klipKustom != null)
            sumberSFX.PlayOneShot(klipKustom);
    }

    public void AturVolumeMusic(float nilaiSlider)
    {
        mainMixer.SetFloat("MusicVol", Mathf.Log10(nilaiSlider) * 20);
        PlayerPrefs.SetFloat("SimpanMusic", nilaiSlider);
    }

    public void AturVolumeSFX(float nilaiSlider)
    {
        mainMixer.SetFloat("SFXVol", Mathf.Log10(nilaiSlider) * 20);
        PlayerPrefs.SetFloat("SimpanSFX", nilaiSlider);
    }
}