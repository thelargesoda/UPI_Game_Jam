using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class PrologManager : MonoBehaviour
{
    [System.Serializable]
    public struct SlideProlog
    {
        public GameObject objekGambar;
        [TextArea(2, 4)]
        public string teksNaratif; // Kosongkan jika hanya menampilkan gambar polos
    }

    [Header("Komponen UI Teks")]
    public GameObject kotakDialog;
    public TMP_Text teksDialog;

    [Header("Daftar Urutan Cerita")]
    public SlideProlog[] daftarSlide;

    [Header("Pengaturan Kecepatan Otomatis")]
    public float kecepatanKetik = 0.04f;      // Jeda antar huruf (makin kecil = makin cepat)
    public float jedaSetelahSelesai = 2.5f;   // Waktu tunggu (detik) setelah teks selesai diketik sebelum ganti slide
    public float durasiGambarPolos = 2.0f;    // Lama tampil jika slide hanya gambar tanpa teks

    [Header("Tujuan Setelah Prolog Selesai")]
    public string sceneBerikutnya = "MainInGame";

    void Start()
    {
        // Langsung jalankan rangkaian prolog otomatis dari awal sampai pindah scene
        StartCoroutine(JalankanPrologOtomatis());
    }

    IEnumerator JalankanPrologOtomatis()
    {
        for (int indeks = 0; indeks < daftarSlide.Length; indeks++)
        {
            // 1. Matikan semua gambar dulu, lalu nyalakan gambar untuk slide sekarang
            for (int i = 0; i < daftarSlide.Length; i++)
            {
                if (daftarSlide[i].objekGambar != null)
                    daftarSlide[i].objekGambar.SetActive(false);
            }

            if (daftarSlide[indeks].objekGambar != null)
                daftarSlide[indeks].objekGambar.SetActive(true);

            // 2. Cek apakah slide ini gambar polos atau ada teks narasinya
            if (string.IsNullOrWhiteSpace(daftarSlide[indeks].teksNaratif))
            {
                kotakDialog.SetActive(false);
                // Tahan gambar polos selama beberapa detik sebelum lanjut otomatis
                yield return new WaitForSeconds(durasiGambarPolos);
            }
            else
            {
                kotakDialog.SetActive(true);
                teksDialog.text = "";

                // 3. Animasi ketik huruf demi huruf (Typewriter)
                foreach (char huruf in daftarSlide[indeks].teksNaratif.ToCharArray())
                {
                    teksDialog.text += huruf;
                    yield return new WaitForSeconds(kecepatanKetik);
                }

                // 4. Tunggu beberapa detik agar pemain sempat selesai membaca sebelum ganti slide
                yield return new WaitForSeconds(jedaSetelahSelesai);
            }
        }

        // 5. Semua slide selesai -> otomatis terbang ke MainInGame!
        SceneManager.LoadScene(sceneBerikutnya);
    }
}