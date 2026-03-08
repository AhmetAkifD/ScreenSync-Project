package com.akif.screensync.mobile_app

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.Service
import android.content.Intent
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
            .setContentText("Ekran PC'ye aktarılıyor...")
            .setSmallIcon(android.R.drawable.ic_menu_camera) // Android'in varsayılan kamera ikonu
            .build()

        // Android'e "Ben bir ekran yakalama servisiyim" diyoruz
        startForeground(1, notification, android.content.pm.ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION)

        return START_NOT_STICKY
    }
}