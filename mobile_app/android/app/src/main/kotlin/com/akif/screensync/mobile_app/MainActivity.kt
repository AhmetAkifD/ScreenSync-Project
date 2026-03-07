package com.akif.screensync.mobile_app

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

class MainActivity: FlutterActivity() {
    private val CHANNEL = "com.akif.screensync/stream"
    private val REQUEST_CODE_CAPTURE = 1001
    private val peers = mutableListOf<WifiP2pDevice>()

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
        mChannel = manager.initialize(this, mainLooper, null)

        intentFilter.addAction(WifiP2pManager.WIFI_P2P_STATE_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_PEERS_CHANGED_ACTION)
        intentFilter.addAction(WifiP2pManager.WIFI_P2P_CONNECTION_CHANGED_ACTION)

        // Kulağımızı (receiver) sisteme kaydediyoruz
        registerReceiver(receiver, intentFilter)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler { call, result ->
            when (call.method) {
                "startCapture" -> {
                    val projectionManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as android.media.projection.MediaProjectionManager
                    startActivityForResult(projectionManager.createScreenCaptureIntent(), REQUEST_CODE_CAPTURE)
                    result.success("İzin penceresi açıldı")
                }
                "startDiscovery" -> {
                    startDiscovery(result)
                }
                else -> result.notImplemented()
            }
        }
    }
    override fun onDestroy() {
        super.onDestroy()
        unregisterReceiver(receiver)
    }

    @RequiresApi(Build.VERSION_CODES.LOLLIPOP)
    private fun requestScreenCapture() {
        val manager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        // Sistemden izin isteme ekranını başlatıyoruz
        startActivityForResult(manager.createScreenCaptureIntent(), CAPTURE_CODE)
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == CAPTURE_CODE && resultCode == RESULT_OK) {
            // İzin alındı! Buradan sonra veriyi paketleyip göndermeye başlayacağız.
            println("Ekran yakalama izni verildi!")
        }
    }
    @RequiresApi(Build.VERSION_CODES.ICE_CREAM_SANDWICH)
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
}