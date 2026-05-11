package com.akif.screensync.mobile_app

import android.Manifest
import android.app.Activity
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.hardware.display.DisplayManager
import android.hardware.display.VirtualDisplay
import android.media.MediaCodec
import android.media.MediaCodecInfo
import android.media.MediaFormat
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.net.NetworkInfo
import android.net.wifi.WpsInfo
import android.net.wifi.p2p.WifiP2pConfig
import android.net.wifi.p2p.WifiP2pDevice
import android.net.wifi.p2p.WifiP2pManager
import android.os.Build
import android.os.Handler
import android.os.Looper
import androidx.annotation.RequiresPermission
import io.flutter.plugin.common.EventChannel
import io.flutter.plugin.common.MethodChannel
import java.io.BufferedReader
import java.io.InputStreamReader
import java.io.PrintWriter
import java.net.Socket
import java.nio.ByteBuffer

class MainActivityTools(private val activity: Activity) {
    val REQUEST_CODE_CAPTURE = 1001

    var eventSink: EventChannel.EventSink? = null

    // --- P2P VE DONANIM DEĞİŞKENLERİ ---
    val peers = mutableListOf<WifiP2pDevice>()
    lateinit var manager: WifiP2pManager
    lateinit var mChannel: WifiP2pManager.Channel
    lateinit var projectionManager: MediaProjectionManager

    // --- VİDEO VE SOKET DEĞİŞKENLERİ ---
    var mediaProjection: MediaProjection? = null
    var encoder: MediaCodec? = null
    var virtualDisplay: VirtualDisplay? = null
    var isStreaming = false
    var targetIpAddress = "192.168.137.1"

    var commandSocket: Socket? = null
    var commandOut: PrintWriter? = null
    var commandIn: BufferedReader? = null

    // Sınıf başlatıldığında Android servislerini ayağa kaldırır
    fun initialize() {
        manager = activity.getSystemService(Context.WIFI_P2P_SERVICE) as WifiP2pManager
        mChannel = manager.initialize(activity, activity.mainLooper, null)
        projectionManager = activity.getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
    }

    // --- 1. AĞ VE CİHAZ KEŞFİ (WIFI P2P) METOTLARI ---

    private val peerListListener = WifiP2pManager.PeerListListener { peerList ->
        val refreshedPeers = peerList.deviceList
        if (refreshedPeers != peers) {
            peers.clear()
            peers.addAll(refreshedPeers)

            println("--- BULUNAN CİHAZLAR ---")
            for (device in peers) {
                println("Cihaz Adı: ${device.deviceName} | MAC Adresi: ${device.deviceAddress}")
                Handler(Looper.getMainLooper()).post {
                    eventSink?.success(mapOf(
                        "type" to "device_found",
                        "deviceName" to device.deviceName,
                        "deviceAddress" to device.deviceAddress
                    ))
                }
            }
        }
    }

    fun createBroadcastReceiver(): BroadcastReceiver {
        return object : BroadcastReceiver() {
            @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
            override fun onReceive(context: Context, intent: Intent) {
                val action: String? = intent.action

                if (WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION == action) {
                    manager.requestPeers(mChannel, peerListListener)
                }
                else if (WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION == action) {
                    val networkInfo = intent.getParcelableExtra<NetworkInfo>(WifiP2pManager.EXTRA_NETWORK_INFO)
                    if (networkInfo != null && networkInfo.isConnected) {
                        println("--- FİZİKSEL BAĞLANTI KURULDU ---")
                        Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "connected")) }
                    } else {
                        println("--- BAĞLANTI KOPTU / BEKLENİYOR ---")
                        Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "disconnected")) }
                    }
                }
            }
        }
    }

    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    fun startDiscovery(result: MethodChannel.Result) {
        manager.discoverPeers(mChannel, object : WifiP2pManager.ActionListener {
            override fun onSuccess() { result.success("Arama başlatıldı") }
            override fun onFailure(reasonCode: Int) { result.error("HATA", "Arama başlatılamadı: $reasonCode", null) }
        })
    }

    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    fun stopDiscovery(result: MethodChannel.Result) {
        manager.stopPeerDiscovery(mChannel, object : WifiP2pManager.ActionListener {
            override fun onSuccess() { result.success("Arama durduruldu") }
            override fun onFailure(reasonCode: Int) { result.error("HATA", "Arama durdurulamadı: $reasonCode", null) }
        })
    }

    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    fun connectToPC(macAddress: String?, result: MethodChannel.Result) {
        if (macAddress == null) {
            result.error("HATA", "MAC adresi boş olamaz!", null)
            return
        }

        val device = peers.find { it.deviceAddress == macAddress }
        if (device == null) {
            result.error("HATA", "Seçilen cihaz bulunamadı!", null)
            return
        }

        val config = WifiP2pConfig().apply {
            deviceAddress = device.deviceAddress
            wps.setup = WpsInfo.PBC
        }

        manager.stopPeerDiscovery(mChannel, object : WifiP2pManager.ActionListener {
            override fun onSuccess() { println("--- Bağlanma öncesi arama durduruldu ---") }
            override fun onFailure(p0: Int) { }
        })

        Handler(Looper.getMainLooper()).post {
            manager.cancelConnect(mChannel, object : WifiP2pManager.ActionListener {
                override fun onSuccess() {
                    Handler(Looper.getMainLooper()).postDelayed({ performActualConnection(config, device.deviceName, result) }, 300)
                }
                override fun onFailure(reasonCode: Int) {
                    Handler(Looper.getMainLooper()).postDelayed({ performActualConnection(config, device.deviceName, result) }, 300)
                }
            })
        }
    }

    @RequiresPermission(allOf = [Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.NEARBY_WIFI_DEVICES])
    private fun performActualConnection(config: WifiP2pConfig, deviceName: String, result: MethodChannel.Result) {
        manager.connect(mChannel, config, object : WifiP2pManager.ActionListener {
            override fun onSuccess() { result.success("Bağlantı isteği PC'ye iletildi") }
            override fun onFailure(reasonCode: Int) { result.error("HATA", "Bağlanılamadı: $reasonCode", null) }
        })
    }

    // --- 2. VİDEO VE SOKET KONTROL METOTLARI ---

    fun connectCommandChannel(ip: String, result: MethodChannel.Result) {
        targetIpAddress = ip
        println("[KOTLIN] Erken bağlantı kuruluyor. Hedef: $targetIpAddress")

        Thread {
            try {
                // Varsa eski zombileri temizle
                try {
                    commandIn?.close()
                    commandOut?.close()
                    commandSocket?.close()
                } catch (e: Exception) { }

                commandSocket = java.net.Socket(targetIpAddress, 50000)
                commandOut = java.io.PrintWriter(commandSocket!!.getOutputStream(), true)
                commandIn = java.io.BufferedReader(java.io.InputStreamReader(commandSocket!!.getInputStream()))

                // 1. Sadece "Ben geldim" de, yayın isteme!
                commandOut?.println("HELO|${Build.MODEL}")
                println("[KOTLIN] PC'ye HELO gönderildi. Arka plan dinlemesi başlıyor...")

                // 2. Artık hep uyanık kal ve PC'den gelecek komutları dinle
                while (true) {
                    val rawResponse = commandIn?.readLine()

                    if (rawResponse == null) {
                        println("[KOTLIN] PC bağlantıyı kesti. Tünel yıkıldı.")
                        Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "disconnected")) }
                        break
                    }

                    val response = rawResponse.trim()

                    if (response == "APPROVE_STREAM") {
                        Handler(Looper.getMainLooper()).post {
                            activity.startActivityForResult(projectionManager.createScreenCaptureIntent(), REQUEST_CODE_CAPTURE)
                        }
                    }
                    else if (response == "REJECT_STREAM") {
                        notifyStreamRejected()
                    }
                    else if (response == "STOP_STREAM") {
                        isStreaming = false
                        Handler(Looper.getMainLooper()).post {
                            mediaProjection?.stop()
                            eventSink?.success(mapOf("type" to "stream_stopped"))
                        }
                    }
                }
            } catch (e: Exception) {
                println("[KOTLIN - HATA] Komut Kanalı patladı: ${e.message}")
            }
        }.start()

        result.success("Komut kanalına bağlanıldı")
    }

    fun startCapture(result: MethodChannel.Result) {
        if (commandSocket == null || commandSocket?.isClosed == true) {
            result.error("HATA", "Önce bağlantı kurulmalı!", null)
            return
        }

        println("[KOTLIN] PC'ye yayın isteği (REQ_STREAM) gönderiliyor...")
        Thread {
            commandOut?.println("REQ_STREAM")
        }.start()

        result.success("İstek işleniyor...")
    }

    fun stopCapture(result: MethodChannel.Result) {
        isStreaming = false
        activity.stopService(Intent(activity, ScreenCaptureService::class.java))
        eventSink?.success(mapOf("type" to "stream_stopped"))
        result.success("Yayın durduruldu")
    }

    fun startVideoStreaming(resultCode: Int, data: Intent) {
        Handler(Looper.getMainLooper()).postDelayed({
            try {
                mediaProjection = projectionManager.getMediaProjection(resultCode, data)

                val width = 720
                val height = 1280
                val dpi = activity.resources.displayMetrics.densityDpi
                val bitrate = 2000000
                val fps = 30

                mediaProjection?.registerCallback(object : MediaProjection.Callback() {
                    override fun onStop() {
                        super.onStop()
                        isStreaming = false
                        activity.stopService(Intent(activity, ScreenCaptureService::class.java))
                        println("--- EKRAN PAYLAŞIMI DURDURULDU ---")
                        try {
                            encoder?.stop()
                            encoder?.release()
                            virtualDisplay?.release()

                            // EKSİK OLAN BİLDİRİM KÖPRÜSÜ EKLENDİ
                            Thread {
                                try {
                                    commandOut?.println("STREAM_STOPPED")
                                    commandSocket?.close()
                                    commandSocket = null
                                } catch (e: Exception) { }
                            }.start()

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

                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) format.setInteger(MediaFormat.KEY_MAX_B_FRAMES, 0)
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) format.setInteger(MediaFormat.KEY_LOW_LATENCY, 1)

                encoder = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
                encoder?.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)

                val inputSurface = encoder?.createInputSurface()
                encoder?.start()

                virtualDisplay = mediaProjection?.createVirtualDisplay(
                    "ScreenSync", width, height, dpi,
                    DisplayManager.VIRTUAL_DISPLAY_FLAG_AUTO_MIRROR,
                    inputSurface, null, null
                )

                isStreaming = true
                println("--- ENCODER BAŞLADI, VİDEO AKIŞI HAZIR ---")

                Thread { streamVideoData() }.start()

                Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "stream_started")) }

            } catch (e: Exception) {
                println("MediaProjection Hatası: ${e.message}")
            }
        }, 500)
    }

    private fun streamVideoData() {
        try {
            val tcpSocket = Socket(targetIpAddress, 50001)
            val outputStream = tcpSocket.getOutputStream()
            val bufferInfo = MediaCodec.BufferInfo()

            while (isStreaming) {
                val outputBufferIndex = encoder?.dequeueOutputBuffer(bufferInfo, 10000) ?: -1
                if (outputBufferIndex >= 0) {
                    val outputBuffer = encoder?.getOutputBuffer(outputBufferIndex)
                    if (outputBuffer != null && bufferInfo.size > 0) {
                        val chunk = ByteArray(bufferInfo.size)
                        outputBuffer.apply {
                            position(bufferInfo.offset)
                            limit(bufferInfo.offset + bufferInfo.size)
                            get(chunk)
                        }

                        val packetData = ByteBuffer.allocate(4 + chunk.size)
                            .putInt(chunk.size).put(chunk).array()

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
        finally {
            // İŞTE KRİTİK NOKTA: Döngü bittiğinde (hata veya manuel durdurma)
            // Flutter'a yayının bittiğini garanti ediyoruz.
            isStreaming = false
            Handler(Looper.getMainLooper()).post {
                eventSink?.success(mapOf("type" to "stream_stopped"))
            }
            println("[KOTLIN] streamVideoData temizlendi ve Flutter uyarıldı.")
        }
    }

    fun notifyStreamRejected() {
        Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "stream_rejected")) }
    }
}