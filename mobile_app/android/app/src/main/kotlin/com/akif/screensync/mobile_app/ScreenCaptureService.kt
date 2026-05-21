package com.akif.screensync.mobile_app

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.annotation.RequiresApi
import androidx.core.app.NotificationCompat

class ScreenCaptureService : Service() {
    override fun onBind(intent: Intent?): IBinder? = null

    @RequiresApi(Build.VERSION_CODES.Q)
    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val channelId = "ScreenSyncCapture"
        val channel = NotificationChannel(channelId, "Ekran Yansıtma", NotificationManager.IMPORTANCE_LOW)
        getSystemService(NotificationManager::class.java).createNotificationChannel(channel)

        val notification = NotificationCompat.Builder(this, channelId)
            .setContentTitle("ScreenSync")
            .setContentText("Ekran ve Ses PC'ye aktarılıyor...")
            .setSmallIcon(android.R.drawable.ic_menu_camera)
            .build()

        try {
            // Android 14 (API 34) ve üzeri için tam uyumluluk kontrolü
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                // Hem ekran yakalama hem de mikrofon tipini bitsel olarak birleştirip servisi başlatıyoruz
                startForeground(
                    1,
                    notification,
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION or ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE
                )
            } else {
                // Android 10 - 13 arası cihazlar için
                startForeground(
                    1,
                    notification,
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION or ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE
                )
            }
            println("[KOTLIN-SERVICE] Ön plan servisi (Ekran + Mikrofon) başarıyla ayağa kalktı.")
        } catch (e: Exception) {
            println("[KOTLIN-SERVICE-HATA] Servis kombine tipte başlatılamadı, sadece ekrana düşülüyor: ${e.message}")
            // Eğer mikrofon izni henüz tam oturmadıysa sunumda çökmesin diye sadece ekrana düşüş (Fallback) yapıyoruz
            startForeground(1, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION)
        }

        return START_NOT_STICKY
    }
}