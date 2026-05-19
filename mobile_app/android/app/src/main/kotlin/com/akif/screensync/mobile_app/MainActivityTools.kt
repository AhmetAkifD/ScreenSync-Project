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
import android.view.Display
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

    // --- EKRAN DÖNDÜRME DEĞİŞKENLERİ ---
    private var displayManager: DisplayManager? = null
    private var lastRotation: Int = -1

    @Volatile private var isEncoderRebuilding = false
    @Volatile var encoder: MediaCodec? = null // Thread'ler arası anında görünmesi için Volatile yapıldı

    // --- VİDEO VE SOKET DEĞİŞKENLERİ ---
    var mediaProjection: MediaProjection? = null
    var virtualDisplay: VirtualDisplay? = null
    var isStreaming = false
    var targetIpAddress = "192.168.137.1"

    var commandSocket: Socket? = null
    var commandOut: PrintWriter? = null
    var commandIn: BufferedReader? = null

    // Ekranın döndüğünü anında yakalayan ajanımız
    private val displayListener = object : DisplayManager.DisplayListener {
        override fun onDisplayAdded(displayId: Int) {}
        override fun onDisplayRemoved(displayId: Int) {}
        override fun onDisplayChanged(displayId: Int) {
            val display = displayManager?.getDisplay(displayId)
            if (display != null && displayId == Display.DEFAULT_DISPLAY) {
                val newRotation = display.rotation
                if (newRotation != lastRotation) {
                    if (lastRotation != -1 && isStreaming) {
                        sendLogToFlutter("[KOTLIN-ROTATION] Ekran dönmesi algılandı! TCP koparılmadan çözünürlük değiştiriliyor...")
                        rebuildEncoder()
                    }
                    lastRotation = newRotation
                }
            }
        }
    }

    fun initialize() {
        manager = activity.getSystemService(Context.WIFI_P2P_SERVICE) as WifiP2pManager
        mChannel = manager.initialize(activity, activity.mainLooper, null)
        projectionManager = activity.getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        displayManager = activity.getSystemService(Context.DISPLAY_SERVICE) as DisplayManager
    }

    fun stopCaptureInternal() {
        if (!isStreaming) return
        isStreaming = false
        displayManager?.unregisterDisplayListener(displayListener)
        activity.stopService(Intent(activity, ScreenCaptureService::class.java))
        Handler(Looper.getMainLooper()).post {
            eventSink?.success(mapOf("type" to "stream_stopped"))
        }
        sendLogToFlutter("[KOTLIN-SİSTEM] Yayın sistem tarafından durduruldu.")
    }

    val screenOffReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            if (intent?.action == Intent.ACTION_SCREEN_OFF && isStreaming) {
                sendLogToFlutter("[KOTLIN-SİSTEM] Ekran kapandı! Çökmeyi önlemek için yayın kesiliyor.")
                stopCaptureInternal()
            }
        }
    }

    // --- 1. AĞ VE CİHAZ KEŞFİ (WIFI P2P) METOTLARI ---
    private val peerListListener = WifiP2pManager.PeerListListener { peerList ->
        val refreshedPeers = peerList.deviceList
        if (refreshedPeers != peers) {
            peers.clear()
            peers.addAll(refreshedPeers)
            for (device in peers) {
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
                } else if (WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION == action) {
                    val networkInfo = intent.getParcelableExtra<NetworkInfo>(WifiP2pManager.EXTRA_NETWORK_INFO)
                    if (networkInfo != null && networkInfo.isConnected) {
                        Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "connected")) }
                    } else {
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
        if (macAddress == null) return
        val device = peers.find { it.deviceAddress == macAddress } ?: return
        val config = WifiP2pConfig().apply { deviceAddress = device.deviceAddress; wps.setup = WpsInfo.PBC }

        manager.stopPeerDiscovery(mChannel, object : WifiP2pManager.ActionListener {
            override fun onSuccess() {} override fun onFailure(p0: Int) {}
        })

        Handler(Looper.getMainLooper()).post {
            manager.cancelConnect(mChannel, object : WifiP2pManager.ActionListener {
                override fun onSuccess() { Handler(Looper.getMainLooper()).postDelayed({ performActualConnection(config, device.deviceName, result) }, 300) }
                override fun onFailure(reasonCode: Int) { Handler(Looper.getMainLooper()).postDelayed({ performActualConnection(config, device.deviceName, result) }, 300) }
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
        Thread {
            try {
                try { commandIn?.close(); commandOut?.close(); commandSocket?.close() } catch (e: Exception) { }

                commandSocket = java.net.Socket(targetIpAddress, 50000)
                commandOut = java.io.PrintWriter(commandSocket!!.getOutputStream(), true)
                commandIn = java.io.BufferedReader(java.io.InputStreamReader(commandSocket!!.getInputStream()))

                commandOut?.println("HELO|${Build.MODEL}")
                while (true) {
                    val rawResponse = commandIn?.readLine()
                    if (rawResponse == null) {
                        Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "disconnected")) }
                        break
                    }

                    when (rawResponse.trim()) {
                        "APPROVE_STREAM" -> {
                            Handler(Looper.getMainLooper()).post {
                                activity.startActivityForResult(projectionManager.createScreenCaptureIntent(), REQUEST_CODE_CAPTURE)
                            }
                        }
                        "REJECT_STREAM" -> notifyStreamRejected()
                        "STOP_STREAM" -> {
                            sendLogToFlutter("[KOTLIN-KOMUT] Masaüstünden STOP_STREAM sinyali alındı.")
                            isStreaming = false
                            Handler(Looper.getMainLooper()).post {
                                mediaProjection?.stop()
                                eventSink?.success(mapOf("type" to "stream_stopped"))
                            }
                        }
                    }
                }
            } catch (e: Exception) {}
        }.start()
        result.success("Komut kanalına bağlanıldı")
    }

    fun startCapture(result: MethodChannel.Result) {
        if (commandSocket == null || commandSocket?.isClosed == true) {
            result.error("HATA", "Önce bağlantı kurulmalı!", null)
            return
        }
        Thread { commandOut?.println("REQ_STREAM") }.start()
        result.success("İstek işleniyor...")
    }

    fun stopCapture(result: MethodChannel.Result) {
        stopCaptureInternal()
        result.success("Yayın durduruldu")
    }

    fun startVideoStreaming(resultCode: Int, data: Intent) {
        Handler(Looper.getMainLooper()).postDelayed({
            try {
                mediaProjection = projectionManager.getMediaProjection(resultCode, data)

                lastRotation = displayManager?.getDisplay(Display.DEFAULT_DISPLAY)?.rotation ?: 0
                displayManager?.registerDisplayListener(displayListener, null)

                val displayMetrics = activity.resources.displayMetrics
                val screenWidth = displayMetrics.widthPixels
                val screenHeight = displayMetrics.heightPixels
                val ratio = screenHeight.toFloat() / screenWidth.toFloat()

                var width = 720
                var height = (width * ratio).toInt()
                if (height % 2 != 0) height += 1

                val dpi = displayMetrics.densityDpi

                mediaProjection?.registerCallback(object : MediaProjection.Callback() {
                    override fun onStop() {
                        super.onStop()
                        isStreaming = false
                        displayManager?.unregisterDisplayListener(displayListener)
                        activity.stopService(Intent(activity, ScreenCaptureService::class.java))
                        try {
                            encoder?.stop(); encoder?.release(); virtualDisplay?.release()
                            Thread { try { commandOut?.println("STREAM_STOPPED"); commandSocket?.close(); commandSocket = null } catch (e: Exception) { } }.start()
                        } catch (e: Exception) { }
                    }
                }, null)

                val format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height)
                format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
                format.setInteger(MediaFormat.KEY_BIT_RATE, 2000000)
                format.setInteger(MediaFormat.KEY_FRAME_RATE, 30)
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
                sendLogToFlutter("[KOTLIN-VİDEO] Yayın başlatıldı. İlk Çözünürlük: ${width}x${height}")

                Thread { streamVideoData() }.start()
                Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "stream_started")) }

            } catch (e: Exception) {
                sendLogToFlutter("[KOTLIN-HATA] startVideoStreaming patladı: ${e.message}")
            }
        }, 500)
    }

    // --- SİHİRLİ METOT: VIRTUAL DISPLAY SİLİNMEZ, YÖNLENDİRİLİR ---
    private fun rebuildEncoder() {
        if (!isStreaming) return
        sendLogToFlutter("[KOTLIN-ROTATION] Yeniden yapılandırma başladı. TCP donduruluyor...")
        isEncoderRebuilding = true

        try {
            encoder?.stop()
            encoder?.release()
            sendLogToFlutter("[KOTLIN-ROTATION] Eski encoder başarıyla temizlendi.")
        } catch (e: Exception) {
            sendLogToFlutter("[KOTLIN-ROTATION-HATA] Eski encoder temizlenirken hata: ${e.message}")
        }

        try {
            val displayMetrics = activity.resources.displayMetrics
            val screenWidth = displayMetrics.widthPixels
            val screenHeight = displayMetrics.heightPixels

            var width = 720
            var height = 1280

            if (screenWidth > screenHeight) {
                height = 720
                width = (height * (screenWidth.toFloat() / screenHeight.toFloat())).toInt()
                if (width % 2 != 0) width += 1
            } else {
                width = 720
                height = (width * (screenHeight.toFloat() / screenWidth.toFloat())).toInt()
                if (height % 2 != 0) height += 1
            }
            sendLogToFlutter("[KOTLIN-ROTATION] Yeni boyutlar hesaplandı: ${width}x${height}")

            val format = MediaFormat.createVideoFormat(MediaFormat.MIMETYPE_VIDEO_AVC, width, height)
            format.setInteger(MediaFormat.KEY_COLOR_FORMAT, MediaCodecInfo.CodecCapabilities.COLOR_FormatSurface)
            format.setInteger(MediaFormat.KEY_BIT_RATE, 2000000)
            format.setInteger(MediaFormat.KEY_FRAME_RATE, 30)
            format.setInteger(MediaFormat.KEY_I_FRAME_INTERVAL, 1)

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) format.setInteger(MediaFormat.KEY_MAX_B_FRAMES, 0)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) format.setInteger(MediaFormat.KEY_LOW_LATENCY, 1)

            val newEncoder = MediaCodec.createEncoderByType(MediaFormat.MIMETYPE_VIDEO_AVC)
            newEncoder.configure(format, null, null, MediaCodec.CONFIGURE_FLAG_ENCODE)
            val inputSurface = newEncoder.createInputSurface()
            newEncoder.start()

            // İŞTE BÜTÜN SIR BURADA: Silmiyoruz, sadece boyutunu ve hedefini güncelliyoruz!
            virtualDisplay?.resize(width, height, displayMetrics.densityDpi)
            virtualDisplay?.surface = inputSurface

            encoder = newEncoder
            sendLogToFlutter("[KOTLIN-ROTATION] Yeni encoder bağlandı, VirtualDisplay yönlendirildi!")

        } catch (e: Exception) {
            sendLogToFlutter("[KOTLIN-ROTATION-CRITICAL] Kodlayıcı yenileme hatası: ${e.message}")
        } finally {
            isEncoderRebuilding = false
            sendLogToFlutter("[KOTLIN-ROTATION] TCP kilidi açıldı, veri akışı devam ediyor.")
        }
    }

    private fun streamVideoData() {
        try {
            val tcpSocket = Socket(targetIpAddress, 50001)
            val outputStream = tcpSocket.getOutputStream()
            val bufferInfo = MediaCodec.BufferInfo()

            sendLogToFlutter("[KOTLIN-TCP] 50001 portuna bağlanıldı, paket gönderimi başlıyor.")

            while (isStreaming) {
                if (isEncoderRebuilding || encoder == null) {
                    Thread.sleep(10)
                    continue
                }

                try {
                    val currentEncoder = encoder
                    if (currentEncoder == null) continue

                    val outputBufferIndex = currentEncoder.dequeueOutputBuffer(bufferInfo, 10000)

                    if (outputBufferIndex == MediaCodec.INFO_OUTPUT_FORMAT_CHANGED) {
                        sendLogToFlutter("[KOTLIN-TCP] Yeni SPS/PPS formatı tespit edildi.")
                        continue
                    }

                    if (outputBufferIndex >= 0) {
                        val outputBuffer = currentEncoder.getOutputBuffer(outputBufferIndex)
                        if (outputBuffer != null && bufferInfo.size > 0) {
                            val chunk = ByteArray(bufferInfo.size)
                            outputBuffer.apply {
                                position(bufferInfo.offset)
                                limit(bufferInfo.offset + bufferInfo.size)
                                get(chunk)
                            }

                            val packetData = ByteBuffer.allocate(4 + chunk.size)
                                .putInt(chunk.size).put(chunk).array()

                            outputStream.write(packetData)
                            outputStream.flush()
                        }
                        currentEncoder.releaseOutputBuffer(outputBufferIndex, false)
                    }
                } catch (e: IllegalStateException) {
                    Thread.sleep(10)
                } catch (e: Exception) {
                    sendLogToFlutter("[KOTLIN-TCP-CRITICAL] TCP Gönderim Hatası (Döngü kırılıyor): ${e.message}")
                    break
                }
            }
            tcpSocket.close()
            sendLogToFlutter("[KOTLIN-TCP] Döngü bitti, soket kapatıldı.")
        } catch (e: Exception) {
            sendLogToFlutter("[KOTLIN-TCP-CRITICAL] Yayın Döngüsü Hatası: ${e.message}")
        }
        finally {
            isStreaming = false
            Handler(Looper.getMainLooper()).post {
                eventSink?.success(mapOf("type" to "stream_stopped"))
            }
        }
    }

    fun notifyStreamRejected() {
        Handler(Looper.getMainLooper()).post { eventSink?.success(mapOf("type" to "stream_rejected")) }
    }

    fun sendLogToFlutter(message: String) {
        // Hem Android'in kendi loglarında (Logcat) görelim
        println(message)

        // Hem de Flutter tarafına gönderelim
        Handler(Looper.getMainLooper()).post {
            eventSink?.success(mapOf("type" to "log", "message" to message))
        }
    }
}