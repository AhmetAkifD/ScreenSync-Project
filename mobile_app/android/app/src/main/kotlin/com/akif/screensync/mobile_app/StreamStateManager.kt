package com.akif.screensync.mobile_app

object StreamStateManager {
    // @Volatile çok önemli: MethodChannel (Ana Thread) ile Ses Döngüsü (Arka Plan Thread)
    // aynı anda bu değişkene eriştiğinde kilitlenme (deadlock) olmasını veya eski değeri okumasını engeller.
    @Volatile
    var isMicMuted = true
}