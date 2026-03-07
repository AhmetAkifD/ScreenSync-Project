package com.akif.screensync.mobile_app

import android.content.Context
import android.content.Intent
import android.media.projection.MediaProjectionManager
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity: FlutterActivity() {
    private val CHANNEL = "com.akif.screensync/stream"
    private val CAPTURE_CODE = 1001

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, CHANNEL).setMethodCallHandler { call, result ->
            if (call.method == "startCapture") {
                requestScreenCapture()
                result.success(null)
            } else {
                result.notImplemented()
            }
        }
    }

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
}