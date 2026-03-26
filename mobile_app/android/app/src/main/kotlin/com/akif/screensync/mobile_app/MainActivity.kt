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
import android.os.Handler
import android.os.Looper
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

    // Flutter'a canlı veri fırlatacağımız hortum
    private var eventSink: io.flutter.plugin.common.EventChannel.EventSink? = null

    // İŞTE SENİN YAPAMADIĞIN 3. ADIM BURADA!
    private val peerListListener = PeerListListener { peerList ->
        val refreshedPeers = peerList.deviceList
        if (refreshedPeers != peers) {
            peers.clear()
            peers.addAll(refreshedPeers)

            println("--- BULUNAN CİHAZLAR ---")
            for (device in peers) {
                println("Cihaz Adı: ${device.deviceName} | MAC Adresi: ${device.deviceAddress}")

                // Cihazı bulduğumuz an EventChannel üzerinden Flutter'a haber uçuruyoruz!
                // Not: EventSink sadece ana UI thread'inde çalışır, o yüzden Handler kullanıyoruz.
                Handler(Looper.getMainLooper()).post {
                    eventSink?.success(mapOf(
                        "type" to "device_found",
                        "deviceName" to device.deviceName,
                        "deviceAddress" to device.deviceAddress
                    ))
                }
            }
            println("------------------------")
        }
        if (peers.isEmpty()) {
            println("Henüz cihaz bulunamadı veya etrafta kimse yok.")
        }
    }

    private val receiver = object : BroadcastReceiver() {
        @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
        override fun onReceive(context: Context, intent: Intent) {
            val action: String? = intent.action

            // 1. Cihazlar bulunduğunda
            if (WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION == action) {
                manager.requestPeers(mChannel, peerListListener)
            }
            // 2. YENİ: Fiziksel Bağlantı Kurulduğunda veya Koptuğunda
            else if (WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION == action) {
                val networkInfo = intent.getParcelableExtra<android.net.NetworkInfo>(WifiP2pManager.EXTRA_NETWORK_INFO)

                if (networkInfo != null && networkInfo.isConnected) {
                    println("--- FİZİKSEL BAĞLANTI KURULDU ---")
                    Handler(Looper.getMainLooper()).post {
                        eventSink?.success(mapOf("type" to "connected"))
                    }
                } else {
                    println("--- BAĞLANTI KOPTU / BEKLENİYOR ---")
                    Handler(Looper.getMainLooper()).post {
                        eventSink?.success(mapOf("type" to "disconnected"))
                    }
                }
            }
        }
    }

    private lateinit var manager: WifiP2pManager
    private lateinit var mChannel: WifiP2pManager.Channel
    private val intentFilter = IntentFilter()

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        manager = getSystemService(Context.WIFI_P2P_SERVICE) as WifiP2pManager
        projectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        mChannel = manager.initialize(this, mainLooper, null)

        intentFilter.addAction(WifiP2pManager.WIFI_P2P_STATE_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION)

        registerReceiver(receiver, intentFilter)

        // Flutter'ın dinleyeceği EventChannel'ı kaydediyoruz
        io.flutter.plugin.common.EventChannel(flutterEngine.dartExecutor.binaryMessenger, "com.akif.screensync/events")
            .setStreamHandler(object : io.flutter.plugin.common.EventChannel.StreamHandler {
                override fun onListen(arguments: Any?, events: io.flutter.plugin.common.EventChannel.EventSink?) {
                    eventSink = events
                }
                override fun onCancel(arguments: Any?) {
                    eventSink = null
                }
            })

        // KANALLARI BİRLEŞTİRDİM! Hepsi tek bir çatının altında toplandı.
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler @androidx.annotation.RequiresPermission(
            allOf = [android.Manifest.permission.ACCESS_FINE_LOCATION, android.Manifest.permission.NEARBY_WIFI_DEVICES]
        ) { call, result ->
            when (call.method) {
                "startCapture" -> {
                    startActivityForResult(projectionManager.createScreenCaptureIntent(), REQUEST_CODE_CAPTURE)
                    result.success("İzin penceresi açıldı")
                }
                "stopCapture" -> {
                    isStreaming = false
                    eventSink?.success(mapOf("type" to "stream_stopped"))
                    result.success("Yayın durduruldu")
                }
                "startDiscovery" -> {
                    startDiscovery(result)
                }
                "stopDiscovery" -> {
                    stopDiscovery(result)
                }
                "connect" -> {
                    // Flutter'dan gönderdiğimiz MAC adresini alıyoruz
                    val macAddress = call.argument<String>("address")
                    connectToPC(macAddress, result)
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
        if (requestCode == REQUEST_CODE_CAPTURE) {
            if (resultCode == Activity.RESULT_OK && data != null) {
                val serviceIntent = Intent(this, ScreenCaptureService::class.java)
                startForegroundService(serviceIntent)

                Handler(Looper.getMainLooper()).postDelayed({
                    try {
                        mediaProjection = projectionManager.getMediaProjection(resultCode, data)
                        println("--- EKRAN YAKALAMA İZNİ ALINDI VE SERVİS BAŞLADI ---")
                        startVideoStreaming()
                    } catch (e: Exception) {
                        println("MediaProjection Hatası: ${e.message}")
                    }
                }, 500)
            } else {
                println("--- KULLANICI EKRAN İZNİNİ REDDETTİ ---")
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        unregisterReceiver(receiver)
    }

    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    private fun startDiscovery(result: MethodChannel.Result) {
        manager.discoverPeers(mChannel, object : WifiP2pManager.ActionListener {
            override fun onSuccess() {
                result.success("Arama başlatıldı")
            }
            override fun onFailure(reasonCode: Int) {
                result.error("HATA", "Arama başlatılamadı: $reasonCode", null)
            }
        })
    }

    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    private fun connectToPC(macAddress: String?, result: MethodChannel.Result) {
        if (macAddress == null) {
            result.error("HATA", "MAC adresi boş olamaz!", null)
            return
        }

        // Listeden, Flutter'ın seçtiği o MAC adresine sahip cihazı bul
        val device = peers.find { it.deviceAddress == macAddress }

        if (device == null) {
            result.error("HATA", "Seçilen cihaz bulunamadı!", null)
            return
        }

        val config = WifiP2pConfig()
        config.deviceAddress = device.deviceAddress
        config.wps.setup = WpsInfo.PBC

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
            } catch (e: Exception) {
                println("UDP Gönderme Hatası: ${e.message}")
            }
        }.start()
    }

    private fun startVideoStreaming() {
        val width = 720
        val height = 1280
        val dpi = resources.displayMetrics.densityDpi
        val bitrate = 2000000 // 2 Mbps
        val fps = 30

        try {
            mediaProjection?.registerCallback(object : MediaProjection.Callback() {
                override fun onStop() {
                    super.onStop()
                    isStreaming = false
                    println("--- EKRAN PAYLAŞIMI DURDURULDU ---")
                    try {
                        encoder?.stop()
                        encoder?.release()
                        virtualDisplay?.release()
                    } catch (e: Exception) {
                        println("Kaynak temizleme hatası: ${e.message}")
                    }
                }
            }, null)

            val format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height)
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
            format.setInteger(MediaFormat.KEY_BIT_RATE, bitrate)
            format.setInteger(MediaFormat.KEY_FRAME_RATE, fps)
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1)

            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.Q) {
                format.setInteger(MediaFormat.KEY_MAX_B_FRAMES, 0)
            }
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
                format.setInteger(MediaFormat.KEY_LOW_LATENCY, 1)
            }

            encoder = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
            encoder?.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)

            val inputSurface = encoder?.createInputSurface()
            encoder?.start()

            virtualDisplay = mediaProjection?.createVirtualDisplay(
                "ScreenSync",
                width, height, dpi,
                DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                inputSurface, null, null
            )

            isStreaming = true
            println("--- ENCODER BAŞLADI, VİDEO AKIŞI HAZIR ---")

            Thread { streamVideoData() }.start()

        } catch (e: Exception) {
            println("Encoder Hatası: ${e.message}")
        }
    }

    private fun streamVideoData() {
        try {
            val pcIpAddress = "192.168.137.1"
            val port = 50000
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

                        val packetData = java.nio.ByteBuffer.allocate(4 + chunk.size)
                            .putInt(chunk.size)
                            .put(chunk)
                            .array()

                        try {
                            outputStream.write(packetData)
                            outputStream.flush()
                        } catch (e: Exception) {
                            println("TCP Gönderim Hatası: ${e.message}")
                            break
                        }
                    }
                    encoder?.releaseOutputBuffer(outputBufferIndex, false)
                }
            }
            tcpSocket.close()

        } catch (e: Exception) {
            println("Yayın Döngüsü Hatası: ${e.message}")
        }
    }
    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    private fun stopDiscovery(result: MethodChannel.Result) {
        manager.stopPeerDiscovery(mChannel, object : WifiP2pManager.ActionListener {
            override fun onSuccess() {
                result.success("Arama durduruldu")
            }
            override fun onFailure(reasonCode: Int) {
                result.error("HATA", "Arama durdurulamadı: $reasonCode", null)
            }
        })
    }
}