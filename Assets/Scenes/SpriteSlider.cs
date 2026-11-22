using UnityEngine;

public class SpriteSlider : MonoBehaviour
{
    public enum JenisAudio { Music, SFX }
    public JenisAudio jenis = JenisAudio.Music;

    private SpriteRenderer srBatang;
    private float batasKiriX;
    private float batasKananX;
    private float lebarAsliX;
    private bool sedangDigeser = false;

    void Start()
    {
        srBatang = GetComponent<SpriteRenderer>();

        // 1. Catat posisi ujung kiri & kanan batang putih saat masih penuh
        batasKiriX = srBatang.bounds.min.x;
        batasKananX = srBatang.bounds.max.x;
        lebarAsliX = transform.localScale.x;

        // 2. Buat area klik transparan tetap di posisi awal agar tidak ikut mengecil
        GameObject areaKlik = new GameObject("AreaKlik_" + gameObject.name);
        areaKlik.transform.position = transform.position;
        BoxCollider2D col = areaKlik.AddComponent<BoxCollider2D>();
        col.size = new Vector2(srBatang.bounds.size.x + 0.4f, srBatang.bounds.size.y * 3f);

        DeteksiKlikSlider deteksi = areaKlik.AddComponent<DeteksiKlikSlider>();
        deteksi.sliderInduk = this;

        // 3. Terapkan nilai volume terakhir yang tersimpan
        string key = (jenis == JenisAudio.Music) ? "SimpanMusic" : "SimpanSFX";
        float nilaiAwal = PlayerPrefs.GetFloat(key, 1f);
        UpdateBatang(nilaiAwal);
    }

    void Update()
    {
        // Selama tombol kiri mouse ditahan setelah klik batang, terus ikuti posisi X mouse!
        if (sedangDigeser)
        {
            if (Input.GetMouseButton(0))
            {
                HitungDanTerapkanVolume();
            }
            else
            {
                sedangDigeser = false; // Lepas klik -> berhenti geser
            }
        }
    }

    public void MulaiGeser()
    {
        sedangDigeser = true;
        HitungDanTerapkanVolume();
    }

    void HitungDanTerapkanVolume()
    {
        Vector3 posisiMouse = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float posisiX = Mathf.Clamp(posisiMouse.x, batasKiriX, batasKananX);
        float nilaiVolume = Mathf.InverseLerp(batasKiriX, batasKananX, posisiX);
        nilaiVolume = Mathf.Clamp(nilaiVolume, 0.0001f, 1f);

        UpdateBatang(nilaiVolume);

        if (AudioManager.instance != null)
        {
            if (jenis == JenisAudio.Music)
                AudioManager.instance.AturVolumeMusic(nilaiVolume);
            else
                AudioManager.instance.AturVolumeSFX(nilaiVolume);
        }
    }

    void UpdateBatang(float nilai)
    {
        Vector3 skala = transform.localScale;
        skala.x = lebarAsliX * nilai;
        transform.localScale = skala;

        float lebarSekarang = (batasKananX - batasKiriX) * nilai;
        transform.position = new Vector3(batasKiriX + (lebarSekarang / 2f), transform.position.y, transform.position.z);
    }
}

public class DeteksiKlikSlider : MonoBehaviour
{
    [HideInInspector] public SpriteSlider sliderInduk;
    void OnMouseDown()
    {
        if (sliderInduk != null) sliderInduk.MulaiGeser();
    }
}