# BAB 4 – Hasil dan Pembahasan

Bagian ini menjelaskan fitur-fitur yang ada di aplikasi Line Balancing. Setiap fitur ditulis dalam 3 paragraf agar mudah dipahami pembaca yang belum familiar dengan istilah teknis.

---

## 1. Dashboard Utama (Publik)

Dashboard adalah halaman pertama yang langsung terbuka ketika aplikasi dijalankan, tanpa perlu login terlebih dahulu. Di bagian atas ada judul aplikasi, informasi status database, dan keterangan waktu pembaruan data. Di bawahnya tampil kartu ringkasan, grafik, dan tabel yang semuanya menampilkan kondisi produksi saat ini. Semua orang, baik tamu maupun pengguna yang sudah login, bisa melihat halaman ini.

Ketika pengguna membuka dashboard, aplikasi otomatis mengambil data terbaru dan menampilkannya. Jika koneksi ke server berjalan lancar, data akan muncul dan status tertulis terhubung. Jika server tidak bisa dihubungi, akan muncul pesan kesalahan dan status tertulis terputus, jadi pengguna langsung tahu apa yang terjadi tanpa harus menebak.

Dashboard dibuat terbuka agar mudah diakses di area produksi. Siapa saja bisa memantau kondisi dengan cepat tanpa langkah tambahan. Namun untuk fitur yang lebih sensitif seperti mencetak laporan, tetap dibatasi hanya untuk admin agar laporan resmi tidak disalahgunakan.

## 2. Filter dan Pencarian Data

Di bawah header terdapat bar filter yang berisi pilihan cell, station, jenis meter, rentang tanggal, dan pencarian nomor seri. Pengguna cukup memilih salah satu atau beberapa filter lalu menekan tombol Terapkan Filter, maka semua bagian di dashboard akan menyesuaikan. Jika pengguna memilih cell tertentu, daftar station otomatis menyesuaikan hanya untuk cell tersebut. Tombol Reset Filter akan muncul jika ada filter yang aktif.

Filter ini bekerja secara menyeluruh. Satu kali filter diterapkan, maka kartu ringkasan, grafik, tabel ringkasan, heatmap, dan data history semuanya ikut terfilter dengan kriteria yang sama. Jika rentang tanggal yang dimasukkan terbalik (tanggal awal lebih besar dari tanggal akhir), sistem akan menolak dan memberi pesan kesalahan.

Hasilnya, pengguna bisa mempersempit data sesuai kebutuhan, misalnya hanya ingin melihat satu cell pada minggu ini atau mencari satu nomor seri yang bermasalah. Fitur pencarian nomor seri sangat membantu untuk melacak unit yang tertahan di salah satu station. Untuk data yang sangat banyak, pencarian sebaiknya tidak terlalu pendek agar hasilnya tetap cepat.

## 3. Kartu Ringkasan (8 Kartu)

Di bagian atas dashboard terdapat delapan kartu yang menampilkan angka-angka penting. Kartu tersebut berisi rata-rata waktu per station, total test, jumlah unit yang sudah dites, jumlah unit yang terdaftar beserta sisa yang menunggu, rata-rata waktu tunggu, tingkat kesibukan, persentase waktu bernilai, dan status takt. Beberapa kartu dilengkapi garis kecil yang menunjukkan perubahan angka dari waktu ke waktu.

Angka-angka ini dihitung dari data log produksi. Misalnya waktu tunggu dihitung dari selisih waktu unit tiba dan waktu mulai dikerjakan, sedangkan tingkat kesibukan dihitung dari total waktu kerja dibagi rentang waktu produksi. Untuk jumlah unit yang sudah dites, sistem menghitung unit yang berbeda agar tidak terhitung ganda.

Kartu-kartu ini dirancang agar pengguna bisa memahami kondisi hanya dengan melihat sekilas. Ada bar kemajuan untuk melihat berapa persen unit yang sudah selesai, seberapa sibuk station, dan seberapa besar porsi waktu yang benar-benar bernilai. Garis kecil di kartu membantu melihat apakah angka naik atau turun setelah filter diubah. Garis tersebut hanya ada selama halaman tidak di-refresh.

## 4. Grafik

Terdapat lima grafik untuk membantu melihat data secara visual. Empat grafik di bagian atas menampilkan rata-rata waktu per station, tren jumlah test, perbandingan waktu bernilai dan tidak bernilai, serta tren tingkat kesibukan dan waktu tunggu. Satu grafik lagi ada di bagian unit flow yang menunjukkan sebaran unit berdasarkan berapa kali sudah dites.

Setiap grafik akan menyesuaikan warna dengan tema yang dipilih (gelap atau terang) dan akan diperbarui setiap kali filter diubah. Jika tidak ada data untuk filter tertentu, grafik tidak akan kosong tanpa keterangan, melainkan muncul pesan bahwa tidak ada data pada periode tersebut.

Grafik sangat membantu untuk menemukan masalah. Misalnya station dengan batang tertinggi biasanya menjadi hambatan, grafik tren menunjukkan beban yang tidak merata, diagram lingkaran menunjukkan seberapa besar waktu terbuang untuk menunggu, dan grafik flow membedakan unit yang baru satu kali test dengan yang sudah lulus lima kali test. Semua grafik menggunakan data yang sama dengan kartu, jadi tidak ada perbedaan angka.

## 5. Ringkasan Per Station dan Klik Untuk Filter

Tabel ringkasan menampilkan semua station dalam satu tabel. Setiap baris berisi nama station, cell, rata-rata waktu, total test, rata-rata tunggu, tingkat kesibukan, waktu bernilai, waktu tidak bernilai, persentase bernilai, dan status takt dengan warna hijau, kuning, atau merah. Di bawah tabel ada baris total yang merangkum keseluruhan. Jika sebuah station tidak memiliki data, akan tertulis N/A bukan angka 0.

Yang menarik, setiap baris bisa diklik. Saat diklik, dashboard akan otomatis memfilter hanya untuk station tersebut. Semua kartu, grafik, dan tabel lain ikut berubah mengikuti filter station yang dipilih. Ini memudahkan pengguna yang menemukan station bermasalah untuk langsung mempersempit analisis tanpa harus mengatur filter manual.

Tabel ini menjadi penghubung antara ringkasan dan detail. Pengguna bisa melihat station mana yang statusnya merah, lalu klik untuk melihat lebih dalam. Namun pengguna perlu memperhatikan bahwa setelah klik, filter station menjadi aktif, sehingga data lain juga ikut menyempit. Indikator filter aktif perlu selalu diperhatikan agar tidak salah menafsirkan data.

## 6. Heatmap Takt per Cell dan Station

Heatmap adalah tampilan kotak-kotak berwarna yang disusun per baris cell. Setiap kotak mewakili satu station dan menampilkan rata-rata waktunya. Warna hijau berarti normal, kuning berarti mendekati batas, merah berarti melebihi batas, dan abu-abu berarti tidak ada data. Jika kursor diarahkan ke kotak, akan muncul informasi nama station, cell, dan statusnya.

Warna ditentukan berdasarkan target waktu yang sudah diatur, misalnya 48 detik. Jika rata-rata di bawah 90% target akan hijau, antara 90-100% kuning, dan di atas 100% merah. Pengguna juga bisa mengatur target ini per cell atau per station sesuai kebutuhan produksi.

Dengan heatmap, ketidakseimbangan langsung terlihat. Dalam satu baris cell, jika ada kotak merah di antara kotak hijau, berarti station tersebut menjadi hambatan di cell itu. Karena heatmap diperbarui bersamaan dengan dashboard, warnanya akan selalu konsisten dengan kartu status takt di bagian atas.

## 7. Alur Unit dari Station ke Station

Bagian ini menunjukkan seberapa jauh unit sudah melewati proses. Tabel atas menampilkan per cell berapa unit yang terdaftar, berapa yang sudah dites, berapa yang masih menunggu, berapa yang sudah lulus 5 station, dan persentase kelulusan. Di samping tabel ada histogram yang membedakan unit yang baru 1 kali test sampai yang sudah 5 kali test dengan warna dari merah ke hijau.

Di bawahnya ada tabel detail yang menampilkan setiap nomor seri, cell-nya, berapa kali sudah dites (misalnya 3/5), dan daftar station yang sudah dilalui. Unit yang belum lengkap akan diberi tanda warna agar mudah dikenali.

Fitur ini menjawab pertanyaan penting: berapa unit yang benar-benar selesai semua tahap dan berapa yang berhenti di tengah. Persentase kelulusan per cell menunjukkan efektifitas lintasan produksi, sedangkan detail per nomor seri memudahkan pelacakan unit yang tertahan. Saat ini hitungan lulus didasarkan pada jumlah station yang dilewati, bukan urutan yang harus berurutan, sehingga masih bisa dikembangkan lagi.

## 8. Data Detail dan Halaman

Tabel detail menampilkan data mentah setiap log produksi, mulai dari station, cell, jenis meter, nomor seri, waktu tiba, waktu mulai, waktu selesai, dan durasi. Di atas tabel tertulis berapa baris yang sedang ditampilkan dari total baris yang ada. Di bawah tabel ada tombol halaman untuk berpindah ke data berikutnya.

Data tidak ditampilkan sekaligus agar aplikasi tetap cepat. Setiap halaman hanya menampilkan 25 baris. Jika pengguna berpindah halaman atau mengubah filter, data akan dimuat ulang sesuai filter tersebut. Jika halaman yang diminta tidak valid, sistem akan memberi pesan kesalahan.

Pembagian halaman membuat aplikasi tetap ringan meskipun data mencapai ratusan log. Pengguna tidak perlu menunggu lama. Karena filter dashboard dan tabel detail terhubung, saat filter diubah, tabel akan kembali ke halaman pertama agar tidak menampilkan halaman kosong.

## 9. Login dan Konfirmasi Identitas

Tombol login ada di pojok kanan atas. Saat ditekan, akan muncul jendela login dengan kolom username dan password, tombol untuk melihat atau menyembunyikan password, serta tombol pintas untuk mengisi akun demo admin atau operator. Setelah berhasil login, nama pengguna dan perannya akan tampil di header beserta tombol logout.

Untuk akun operator yang baru dibuat, setelah login akan muncul jendela tambahan untuk konfirmasi identitas. Operator diminta memasukkan kembali password untuk memastikan akun benar-benar miliknya. Jika password salah, akan muncul pesan gagal. Jika benar, akses dashboard akan dibuka dan status konfirmasi disimpan.

Alur ini dibuat agar akun operator tidak bisa langsung dipakai tanpa verifikasi ulang. Ini penting karena operator hanya boleh melihat data cell tertentu. Jika konfirmasi belum dilakukan, sistem akan menolak akses ke data dashboard meskipun sudah login, sehingga keamanan tetap terjaga bukan hanya di tampilan tapi juga di dalam sistem.

## 10. Mode Operator yang Terkunci Satu Cell

Jika pengguna login sebagai operator, akan muncul banner yang memberitahu bahwa mode operator aktif untuk cell tertentu. Pada bar filter, pilihan cell akan terkunci dan tidak bisa diganti. Operator hanya bisa mengganti filter lain seperti station, jenis meter, atau tanggal, tetapi tetap dalam lingkup cell yang sudah ditentukan.

Pembatasan ini tidak hanya di tampilan, tapi juga dijaga di dalam sistem. Jika operator mencoba mengubah filter cell secara manual, sistem akan tetap memaksa data hanya dari cell yang menjadi haknya. Daftar cell dan station yang tampil juga hanya yang sesuai dengan penugasannya.

Mode ini cocok untuk pabrik yang memiliki beberapa cell terpisah. Setiap operator hanya melihat data miliknya sehingga tidak terjadi kebingungan atau kebocoran data antar line. Jika penugasan cell diubah oleh admin, operator perlu login ulang agar perubahan tersebut berlaku.

## 11. Cetak Laporan Excel dan PDF

Tombol cetak Excel dan PDF hanya muncul untuk admin. Ketika ditekan, sistem akan membuat file laporan berdasarkan filter yang sedang aktif. Jadi jika admin sedang melihat data satu cell pada periode tertentu, laporan yang dihasilkan juga hanya untuk data tersebut. File akan otomatis terunduh dengan nama yang sudah ditentukan.

Laporan Excel berisi beberapa lembar kerja yang terdiri dari ringkasan, ringkasan per station, alur unit, dan detail per unit. Laporan PDF berisi beberapa bagian dengan isi yang serupa. Jika filter tidak valid atau sesi login habis, akan muncul pesan kesalahan yang jelas, misalnya hanya admin yang boleh mencetak atau filter tidak valid.

Pembatasan hanya untuk admin memastikan laporan resmi hanya dibuat oleh pihak yang berwenang. Karena laporan mengambil data yang sama dengan yang tampil di layar, isi laporan akan konsisten dengan dashboard. Kekurangannya, pengguna harus mengunduh file untuk melihat hasilnya karena belum ada pratinjau langsung di dalam aplikasi.

## 12. Sinkronisasi dan Status Database

Aplikasi memiliki fitur untuk menarik data terbaru dari database produksi ke database analitik tanpa perlu restart aplikasi. Fitur ini khusus untuk admin. Selain itu ada pengecekan status koneksi yang hasilnya ditampilkan sebagai badge terhubung atau terputus di header dashboard.

Sistem hanya membaca data dari database produksi, tidak pernah mengubah atau menghapus data di sana. Jika koneksi ke database produksi tidak tersedia, aplikasi tetap berjalan menggunakan data cadangan yang sudah ada, sehingga dashboard tidak kosong dan masih bisa digunakan untuk demo atau pengecekan.

Dengan adanya sinkronisasi manual, data analitik bisa diperbarui kapan saja oleh admin. Namun karena belum ada jadwal otomatis, pembaruan harus dilakukan secara manual. Untuk ke depan, akan lebih baik jika ada penjadwalan otomatis dan informasi kapan terakhir kali sinkronisasi dilakukan agar pengguna tahu seberapa segar data yang dilihat.

## 13. Tema dan Pembaruan Otomatis

Di header terdapat tombol untuk mengganti tema gelap dan terang. Pilihan tema akan disimpan sehingga saat aplikasi dibuka kembali, tema yang terakhir dipakai akan tetap aktif. Perubahan tema juga langsung diterapkan ke grafik agar warnanya tetap nyaman dilihat.

Selain itu ada fitur pembaruan otomatis. Pengguna bisa mengaktifkan auto-refresh dan memilih interval 15, 30, atau 60 detik. Saat aktif, dashboard akan memuat ulang data secara berkala tanpa perlu menekan tombol. Di samping badge status juga ada keterangan berapa lama data terakhir diperbarui.

Kedua fitur ini membuat pemantauan lebih nyaman. Tema gelap membantu mengurangi kelelahan mata di ruangan produksi, sedangkan pembaruan otomatis menjaga data tetap segar tanpa perlu interaksi manual. Interval 15 detik cocok untuk line yang cepat, sedangkan 30 detik adalah pilihan seimbang antara kesegaran data dan beban server. Filter yang sedang aktif tetap berlaku selama pembaruan otomatis berjalan.

---

*Penomoran gambar dan tabel untuk BAB 4 menyesuaikan template laporan kampus.*
