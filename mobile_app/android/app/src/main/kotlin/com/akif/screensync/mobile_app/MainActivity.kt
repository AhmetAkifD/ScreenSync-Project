package com.akif.screensync.mobile_app

import android.Manifest
import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel
import android.content.BroadcastReceiver
import android.net.wifi.p2p.WifiP2pDeviceList
import android.net.wifi.p2p.WifiP2pManager
import android.content.IntentFilter
import android.os.Build
import androidx.annotation.RequiresApi
import android.net.wifi.p2p.WifiP2pDevice
import android.net.wifi.p2p.WifiP2pManager.PeerListListener
import android.net.wifi.WpsInfo
import android.net.wifi.p2p.WifiP2pConfig
import androidx.annotation.RequiresPermission
import android.media.projection.MediaProjection
import android.app.Activity
import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaFormat
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.InetAddress
class MainActivity: FlutterActivity() {
    private val CHANNEL = "com.akif.screensync/stream"
    private val REQUEST_CODE_CAPTURE = 1001
    private val peers = mutableListOf<WifiP2pDevice>()
    private lateinit var projectionManager: MediaProjectionManager
    private var mediaProjection: MediaProjection? = null
    private var encoder: MediaCodec? = null
    private var virtualDisplay: VirtualDisplay? = null
    private var isStreaming = false
    private var udpSocket: DatagramSocket? = null

    @RequiresApi(Build.VERSION_CODES.ICE_CREAM_SANDWICH)
    private val peerListListener = PeerListListener { peerList ->
        val refreshedPeers = peerList.deviceList
        if (refreshedPeers != peers) {
            peers.clear()
            peers.addAll(refreshedPeers)

            // Cihazları direkt log ekranına basıyoruz
            println("--- BULUNAN CİHAZLAR ---")
            for (device in peers) {
                println("Cihaz Adı: ${device.deviceName} | MAC Adresi: ${device.deviceAddress}")
            }
            println("------------------------")
        }
        if (peers.isEmpty()) {
            println("Henüz cihaz bulunamadı veya etrafta kimse yok.")
        }
    }

    private val receiver = object : BroadcastReceiver() {
        @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
        @RequiresApi(Build.VERSION_CODES.ICE_CREAM_SANDWICH)
        override fun onReceive(context: Context, intent: Intent) {
            val action: String? = intent.action
            // Eğer sistem "yeni cihazlar buldum" derse, listeyi istiyoruz
            if (WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION == action) {
                manager.requestPeers(mChannel, peerListListener)
            }
        }
    }
    private lateinit var manager: WifiP2pManager
    private lateinit var mChannel: WifiP2pManager.Channel
    private val intentFilter = IntentFilter()
    private val CAPTURE_CODE = 1001

    @RequiresApi(Build.VERSION_CODES.LOLLIPOP)
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        manager = getSystemService(Context.WIFI_P2P_SERVICE) as WifiP2pManager
        projectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        mChannel = manager.initialize(this, mainLooper, null)

        intentFilter.addAction(WifiP2pManager.WIFI_P2P_STATE_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION)

        // Kulağımızı (receiver) sisteme kaydediyoruz
        registerReceiver(receiver, intentFilter)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler @androidx.annotation.RequiresPermission(
            allOf = [android.Manifest.permission.ACCESS_FINE_LOCATION, android.Manifest.permission.NEARBY_WIFI_DEVICES]
        ) { call, result ->
            when (call.method) {
                "startCapture" -> {
                    val projectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
                    startActivityForResult(projectionManager.createScreenCaptureIntent(), REQUEST_CODE_CAPTURE)
                    result.success("İzin penceresi açıldı")
                }
                "startDiscovery" -> {
                    startDiscovery(result)
                }
                "connect" -> {
                    connectToPC(result)
                }
                "sendTest" -> {
                    sendUdpTestMessage("Merhaba PC! Tünel sapasağlam.")
                    result.success("Test gönderildi")
                }
                else -> result.notImplemented()
            }
        }
    }
    @RequiresApi(Build.VERSION_CODES.O)
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)

        // Bizim gönderdiğimiz 1001 kodlu ekran yakalama isteği mi dönmüş?
        if (requestCode == REQUEST_CODE_CAPTURE) {
            if (resultCode == Activity.RESULT_OK && data != null) {
                // Servisi başlatıyoruz
                val serviceIntent = Intent(this, ScreenCaptureService::class.java)
                startForegroundService(serviceIntent)

                // YENİ: Yarış Durumu (Race Condition) Çözümü
                // startForegroundService asenkron olduğu için servisin ayağa kalkıp
                // startForeground komutunu işlemesini beklememiz gerekiyor.
                // Aksi takdirde Android 14 anında çöktürür.
                android.os.Handler(android.os.Looper.getMainLooper()).postDelayed({
                    try {
                        mediaProjection = projectionManager.getMediaProjection(resultCode, data)
                        println("--- EKRAN YAKALAMA İZNİ ALINDI VE SERVİS BAŞLADI ---")
                        startVideoStreaming() // YENİ: İzni aldığımız an akışı başlat
                    } catch (e: Exception) {
                        println("MediaProjection Hatası: ${e.message}")
                    }
                }, 500) // Servise ayağa kalkması için 500 milisaniye (yarım saniye) süre veriyoruz

            } else {
                println("--- KULLANICI EKRAN İZNİNİ REDDETTİ ---")
            }
        }
    }
    override fun onDestroy() {
        super.onDestroy()
        unregisterReceiver(receiver)
    }
    private fun requestScreenCapture() {
        val manager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        // Sistemden izin isteme ekranını başlatıyoruz
        startActivityForResult(manager.createScreenCaptureIntent(), CAPTURE_CODE)
    }
    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    private fun startDiscovery(result: MethodChannel.Result) {
        manager.discoverPeers(mChannel, @RequiresApi(Build.VERSION_CODES.ICE_CREAM_SANDWICH)
        object : WifiP2pManager.ActionListener {
            override fun onSuccess() {
                result.success("Arama başlatıldı")
            }
            override fun onFailure(reasonCode: Int) {
                result.error("HATA", "Arama başlatılamadı: $reasonCode", null)
            }
        })
    }
    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    private fun connectToPC(result: MethodChannel.Result) {
        if (peers.isEmpty()) {
            result.error("HATA", "Önce arama yapıp PC'yi bulmalısın!", null)
            return
        }

        // Listede bulduğumuz ilk cihaza (Yani senin bilgisayarına) bağlanıyoruz
        val device = peers[0]
        val config = WifiP2pConfig()
        config.deviceAddress = device.deviceAddress
        config.wps.setup = WpsInfo.PBC // Standart buton basma yöntemiyle eşleş

        manager.connect(mChannel, config, object : WifiP2pManager.ActionListener {
            override fun onSuccess() {
                println("--- BAĞLANTI İSTEĞİ GÖNDERİLDİ: ${device.deviceName} ---")
                result.success("Bağlantı isteği PC'ye iletildi")
            }

            override fun onFailure(reasonCode: Int) {
                println("--- BAĞLANTI İSTEĞİ BAŞARISIZ (Kod: $reasonCode) ---")
                result.error("HATA", "Bağlanılamadı: $reasonCode", null)
            }
        })
    }

    private fun sendUdpTestMessage(message: String) {
        // Ağ işlemleri ana thread'de yapılamaz, çökmemesi için Thread açıyoruz
        Thread {
            try {
                val pcIpAddress = "192.168.137.1"
                val port = 50000

                val socket = java.net.DatagramSocket()
                val buffer = message.toByteArray(Charsets.UTF_8)
                val serverAddress = java.net.InetAddress.getByName(pcIpAddress)

                val packet = java.net.DatagramPacket(buffer, buffer.size, serverAddress, port)
                socket.send(packet)
                socket.close()

                println("Test mesajı fırlatıldı: $message")
            } catch (e: Exception) {
                println("UDP Gönderme Hatası: ${e.message}")
            }
        }.start()
    }
    private fun startVideoStreaming() {
        // Çözünürlük ve kalite ayarları
        val width = 720
        val height = 1280
        val dpi = resources.displayMetrics.densityDpi
        val bitrate = 2000000 // 2 Mbps
        val fps = 30

        try {
            // YENİ EKLENEN KISIM: Android 14 Callback Zorunluluğu
            // Sistemi dinliyoruz, kullanıcı yayını keserse kaynakları temizleyeceğiz
            mediaProjection?.registerCallback(object : MediaProjection.Callback() {
                override fun onStop() {
                    super.onStop()
                    isStreaming = false
                    println("--- EKRAN PAYLAŞIMI SİSTEM/KULLANICI TARAFINDAN DURDURULDU ---")

                    try {
                        encoder?.stop()
                        encoder?.release()
                        virtualDisplay?.release()
                    } catch (e: Exception) {
                        println("Kaynak temizleme hatası: ${e.message}")
                    }
                }
            }, null)

            // 1. H.264 Encoder Ayarları
            val format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height)
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
            format.setInteger(MediaFormat.KEY_BIT_RATE, bitrate)
            format.setInteger(MediaFormat.KEY_FRAME_RATE, fps)
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1) // Saniyede 1 Keyframe (Anahtar Kare)
            // YENİ: Sıfır Gecikme Optimizasyonları
            // B-Frame'leri iptal ediyoruz (Android 10 ve üstü)
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
                format.setInteger(MediaFormat.KEY_MAX_B_FRAMES, 0)
            }
            // İşletim sistemini donanımsal düşük gecikme moduna zorluyoruz (Android 11 ve üstü)
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
                format.setInteger(MediaFormat.KEY_LOW_LATENCY, 1)
            }
            encoder = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
            encoder?.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)

            // 2. Encoder'ın giriş kapısını alıyoruz ve başlatıyoruz
            val inputSurface = encoder?.createInputSurface()
            encoder?.start()

            // 3. Ekran piksellerini Encoder'a yönlendiren Sanal Ekranı kuruyoruz
            virtualDisplay = mediaProjection?.createVirtualDisplay(
                "ScreenSync",
                width, height, dpi,
                DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                inputSurface, null, null
            )

            isStreaming = true
            println("--- ENCODER BAŞLADI, VİDEO AKIŞI HAZIR ---")

            // 4. Çıkan verileri okuyup UDP'den gönderecek işçiyi (Thread) başlatıyoruz
            Thread { streamVideoData() }.start()

        } catch (e: Exception) {
            println("Encoder Hatası: ${e.message}")
        }
    }

    private fun streamVideoData() {
        try {
            val pcIpAddress = "192.168.137.1"
            val port = 50000

            // YENİ: UDP (DatagramSocket) yerine TCP (Socket) kullanıyoruz
            // Bu satır çalıştığında PC'deki C# uygulamasına doğrudan kalıcı bir boru bağlanır
            val tcpSocket = java.net.Socket(pcIpAddress, port)
            val outputStream = tcpSocket.getOutputStream()

            val bufferInfo = android.media.MediaCodec.BufferInfo()

            while (isStreaming) {
                val outputBufferIndex = encoder?.dequeueOutputBuffer(bufferInfo, 10000) ?: -1

                if (outputBufferIndex >= 0) {
                    val outputBuffer = encoder?.getOutputBuffer(outputBufferIndex)

                    if (outputBuffer != null && bufferInfo.size > 0) {
                        val chunk = ByteArray(bufferInfo.size)
                        outputBuffer.position(bufferInfo.offset)
                        outputBuffer.limit(bufferInfo.offset + bufferInfo.size)
                        outputBuffer.get(chunk)

                        // 4 Byte Boyut Başlığı + H.264 Verisi (Scrcpy Taktiği)
                        val packetData = java.nio.ByteBuffer.allocate(4 + chunk.size)
                            .putInt(chunk.size)
                            .put(chunk)
                            .array()

                        try {
                            // YENİ: TCP üzerinden veriyi akıtıyoruz. EMSGSIZE sınırı artık yok!
                            outputStream.write(packetData)
                            outputStream.flush()
                        } catch (e: Exception) {
                            println("TCP Gönderim Hatası (Bağlantı kopmuş olabilir): ${e.message}")
                            break // Hata varsa döngüden çık
                        }
                    }
                    encoder?.releaseOutputBuffer(outputBufferIndex, false)
                }
            }

            // Döngü bitince boruyu temizle
            tcpSocket.close()

        } catch (e: Exception) {
            println("Yayın Döngüsü Hatası: ${e.message}")
        }
    }
}