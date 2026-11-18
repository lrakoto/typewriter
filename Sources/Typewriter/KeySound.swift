import AVFoundation

/// Short, original synthesized clicks. No bundled recordings or network audio.
final class KeySound {
    static let shared = KeySound()
    private let engine = AVAudioEngine()
    private let player = AVAudioPlayerNode()
    private let format = AVAudioFormat(standardFormatWithSampleRate: 44100, channels: 1)!
    private var clicks: [AVAudioPCMBuffer] = []
    private var carriage: AVAudioPCMBuffer!

    private init() {
        engine.attach(player)
        engine.connect(player, to: engine.mainMixerNode, format: format)
        clicks = (0..<5).map { makeClick(seed: UInt32($0 + 41), isReturn: false) }
        carriage = makeClick(seed: 82, isReturn: true)
    }

    func play(returnKey: Bool, volume: Float) {
        guard volume > 0 else { return }
        do {
            if !engine.isRunning { try engine.start() }
            player.volume = volume
            if !player.isPlaying { player.play() }
            player.scheduleBuffer(returnKey ? carriage : clicks.randomElement()!, completionHandler: nil)
        } catch { /* Audio output may be unavailable; writing remains uninterrupted. */ }
    }

    private func makeClick(seed: UInt32, isReturn: Bool) -> AVAudioPCMBuffer {
        let length: AVAudioFrameCount = isReturn ? 6500 : 1900
        let buffer = AVAudioPCMBuffer(pcmFormat: format, frameCapacity: length)!
        buffer.frameLength = length
        var state = seed
        for index in 0..<Int(length) {
            let t = Double(index) / 44100
            state = 1664525 &* state &+ 1013904223
            let noise = Double(state) / Double(UInt32.max) * 2 - 1
            let attack = min(1, t / 0.001)
            let body = sin(t * 2 * .pi * (isReturn ? 210 : 760 + Double(seed) * 6)) * exp(-t * 110)
            let click = noise * exp(-t * (isReturn ? 48 : 190))
            let bell = isReturn ? sin(t * 2 * .pi * 2400) * exp(-t * 25) * 0.15 : 0
            buffer.floatChannelData![0][index] = Float((click * 0.55 + body * 0.25 + bell) * attack)
        }
        return buffer
    }
}
