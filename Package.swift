// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Typewriter",
    platforms: [.macOS(.v14)],
    products: [.executable(name: "Typewriter", targets: ["Typewriter"])],
    targets: [
        .target(name: "TypewriterCore"),
        .executableTarget(name: "Typewriter", dependencies: ["TypewriterCore"]),
        .executableTarget(name: "TypewriterChecks", dependencies: ["TypewriterCore"], path: "Tests/TypewriterCoreTests")
    ],
    swiftLanguageModes: [.v5]
)
