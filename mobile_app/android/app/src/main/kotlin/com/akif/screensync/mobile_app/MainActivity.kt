package com.akif.screensync.mobile_app

import android.app.Activity
import android.content.BroadcastReceiver
import android.content.Intent
import android.content.IntentFilter
import android.net.wifi.p2p.WifiP2pManager
import android.os.Build
import androidx.annotation.RequiresApi
import com.akif.screensync.mobile_app.MainActivityTools
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.EventChannel
import io.flutter.plugin.common.MethodChannel

class MainActivity: FlutterActivity() {
    private val CHANNEL = "com.akif.screensync/stream"
    private val EVENTS = "com.akif.screensync/events"

    // İş mantığı sınıfımız
    private lateinit var tools: MainActivityTools
    private var receiver: BroadcastReceiver? = null
    private val intentFilter = IntentFilter()
    var isMicMuted = true

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        // Tools sınıfını başlatıyoruz
        tools = MainActivityTools(this)
        tools.initialize()

        // P2P Dinleyicilerini Kur
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_STATE_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION)

        receiver = tools.createBroadcastReceiver()
        registerReceiver(receiver, intentFilter)

        // 1. Flutter'a Veri Gönderme Kanalı (EventChannel)
        EventChannel(flutterEngine.dartExecutor.binaryMessenger, EVENTS)
            .setStreamHandler(object : EventChannel.StreamHandler {
                override fun onListen(arguments: Any?, events: EventChannel.EventSink?) {
                    tools.eventSink = events
                }
                override fun onCancel(arguments: Any?) {
                    tools.eventSink = null
                }
            })

        // 2. Flutter'dan Gelen İstekleri Karşılama Kanalı (MethodChannel)
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "updateStreamSettings" -> {
                    val bitrate = call.argument<Int>("bitrate") ?: 4
                    val resolution = call.argument<String>("resolution") ?: "720p"
                    tools.updateStreamSettings(bitrate, resolution, result)
                }
                "isUsbConnected" -> {
                    val batteryIntent = registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
                    val chargePlug = batteryIntent?.getIntExtra(android.os.BatteryManager.EXTRA_PLUGGED, -1) ?: -1
                    val usbCharge = chargePlug == android.os.BatteryManager.BATTERY_PLUGGED_USB
                    result.success(usbCharge)
                }
                "connectCommand" -> tools.connectCommandChannel(call.argument<String>("ip") ?: "127.0.0.1", result)
                "startCapture" -> tools.startCapture(result)
                "stopCapture" -> tools.stopCapture(result)
                "startDiscovery" -> tools.startDiscovery(result)
                "stopDiscovery" -> tools.stopDiscovery(result)
                "connect" -> tools.connectToPC(call.argument<String>("address"), result)
                "toggleMicrophone" -> {
                    val isMuted = call.argument<Boolean>("isMuted") ?: true

                    // Sinyali direkt tekil merkeze yazıyoruz
                    StreamStateManager.isMicMuted = isMuted

                    println(if (isMuted) "[KOTLIN-SES] Mikrofon SESSİZE alındı." else "[KOTLIN-SES] Mikrofon AÇILDI.")
                    result.success(null)
                }
                else -> result.notImplemented()
            }
        }
    }

    // Ekran Kayıt Onay Penceresi (Intent) Sonuçlandığında
    @RequiresApi(Build.VERSION_CODES.O)
    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)

        if (requestCode == tools.REQUEST_CODE_CAPTURE) {
            if (resultCode == Activity.RESULT_OK && data != null) {
                // Ön plan servisini başlat ve videoyu akıtmaya başla
                val serviceIntent = Intent(this, ScreenCaptureService::class.java)
                startForegroundService(serviceIntent)

                tools.startVideoStreaming(resultCode, data)
            } else {
                println("--- KULLANICI EKRAN İZNİNİ REDDETTİ ---")
                tools.notifyStreamRejected()
            }
        }
    }

    override fun onDestroy() {
        super.onDestroy()
        unregisterReceiver(receiver)
    }

    override fun onResume() {
        super.onResume()
        // Ekran kapanma dinleyicisini aktif et
        val filter = IntentFilter(Intent.ACTION_SCREEN_OFF)
        registerReceiver(tools.screenOffReceiver, filter)
    }

    override fun onPause() {
        super.onPause()
        // Uygulama arka plana geçtiğinde dinleyiciyi kaldır (Hafıza sızıntısını önler)
        try {
            unregisterReceiver(tools.screenOffReceiver)
        } catch (e: Exception) {
            // Zaten kayıtlı değilse hata vermesini engelle
        }
    }
}