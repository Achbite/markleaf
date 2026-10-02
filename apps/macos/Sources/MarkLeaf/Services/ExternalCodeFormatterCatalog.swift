import Foundation

struct ExternalCodeFormatterTool: Equatable {
    let id: String
    let displayName: String
    let languages: [String]
    let executableName: String
    let arguments: [String]
    let selectionLineRangePrefix: String?
    let homepageURL: URL
}

struct ExternalCodeFormatterLaunch: Equatable {
    let executablePath: String
    let arguments: [String]
    let environment: [String: String]
}

enum ExternalCodeFormatterAvailability: Equatable {
    case available(ExternalCodeFormatterLaunch)
    case notInstalled
    case jarMissingJavaRuntime
    case latexMissingPerlRuntime
    case invalidCustomPath
}

enum ExternalCodeFormatterJavaRuntimeLookup {
    case automatic
    case candidates([String])
}

enum ExternalCodeFormatterPerlModuleLookup {
    case automatic
    case present
    case missing
}

enum ExternalCodeFormatterCatalog {
    private static let baseTools: [ExternalCodeFormatterTool] = [
        ExternalCodeFormatterTool(
            id: "black",
            displayName: "Black",
            languages: ["python", "py"],
            executableName: "black",
            arguments: ["--stdin-filename", "input.py", "-"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://black.readthedocs.io/en/stable/getting_started.html")!
        ),
        ExternalCodeFormatterTool(
            id: "google-java-format",
            displayName: "google-java-format",
            languages: ["java"],
            executableName: "google-java-format",
            arguments: ["-"],
            selectionLineRangePrefix: "--lines=",
            homepageURL: URL(string: "https://github.com/google/google-java-format/releases/latest")!
        ),
        ExternalCodeFormatterTool(
            id: "clang-format",
            displayName: "clang-format",
            languages: ["c", "cpp", "c++", "cc", "cxx", "hpp", "objective-c", "objc", "m", "mm"],
            executableName: "clang-format",
            arguments: ["--assume-filename=input.c"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://clang.llvm.org/docs/ClangFormat.html")!
        ),
        ExternalCodeFormatterTool(
            id: "verible-verilog-format",
            displayName: "Verible Verilog Format",
            languages: ["verilog", "sv", "systemverilog"],
            executableName: "verible-verilog-format",
            arguments: ["-"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://github.com/chipsalliance/verible/releases/latest")!
        ),
        ExternalCodeFormatterTool(
            id: "latexindent",
            displayName: "latexindent",
            languages: ["latex", "tex"],
            executableName: "latexindent",
            arguments: ["-"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://github.com/cmhughes/latexindent.pl/releases/latest")!
        ),
        ExternalCodeFormatterTool(
            id: "rustfmt",
            displayName: "rustfmt",
            languages: ["rust", "rs"],
            executableName: "rustfmt",
            arguments: ["--emit", "stdout"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://github.com/rust-lang/rustfmt")!
        ),
        ExternalCodeFormatterTool(
            id: "gofmt",
            displayName: "gofmt",
            languages: ["go"],
            executableName: "gofmt",
            arguments: [],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://go.dev/dl/")!
        ),
        ExternalCodeFormatterTool(
            id: "sqlfluff",
            displayName: "sqlfluff",
            languages: ["sql"],
            executableName: "sqlfluff",
            arguments: ["-"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://docs.sqlfluff.com/en/stable/gettingstarted.html")!
        ),
        ExternalCodeFormatterTool(
            id: "shfmt",
            displayName: "shfmt",
            languages: ["shell", "sh", "bash"],
            executableName: "shfmt",
            arguments: [],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://github.com/mvdan/sh")!
        ),
        ExternalCodeFormatterTool(
            id: "taplo",
            displayName: "taplo",
            languages: ["toml"],
            executableName: "taplo",
            arguments: ["format", "-"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://taplo.tamasfe.dev/")!
        ),
        ExternalCodeFormatterTool(
            id: "xmllint",
            displayName: "xmllint",
            languages: ["xml"],
            executableName: "xmllint",
            arguments: ["--format", "-"],
            selectionLineRangePrefix: nil,
            homepageURL: URL(string: "https://gitlab.gnome.org/GNOME/libxml2/-/wikis/home")!
        ),
    ]

    static let displayTools: [ExternalCodeFormatterTool] = baseTools
        .sorted { lhs, rhs in
            let lhsRank = CodeBlockLanguageCatalog.formatterDisplayRank(forLanguages: lhs.languages)
            let rhsRank = CodeBlockLanguageCatalog.formatterDisplayRank(forLanguages: rhs.languages)
            if lhsRank != rhsRank { return lhsRank < rhsRank }
            return lhs.displayName.localizedCaseInsensitiveCompare(rhs.displayName) == .orderedAscending
        }

    static var tools: [ExternalCodeFormatterTool] {
        displayTools.map { tool in
            ExternalCodeFormatterTool(
                id: tool.id,
                displayName: tool.displayName,
                languages: CodeBlockLanguageCatalog.sortedForDisplay(tool.languages),
                executableName: tool.executableName,
                arguments: tool.arguments,
                selectionLineRangePrefix: tool.selectionLineRangePrefix,
                homepageURL: tool.homepageURL
            )
        }
    }

    static func tool(language rawLanguage: String) -> ExternalCodeFormatterTool? {
        let language = rawLanguage.trimmingCharacters(in: .whitespacesAndNewlines).lowercased()
        return tools.first { $0.languages.contains(language) }
    }

    static func processArguments(
        for tool: ExternalCodeFormatterTool,
        selectionLineRange: ClosedRange<Int>?,
        sqlDialect: SQLFormatterDialect = .ansi
    ) -> [String] {
        guard let selectionLineRange, let prefix = tool.selectionLineRangePrefix else {
            guard tool.id == "sqlfluff" else { return tool.arguments }
            return ["format", "--dialect", sqlDialect.rawValue] + tool.arguments
        }
        guard tool.id != "sqlfluff" else { return tool.arguments }
        return [prefix + "\(selectionLineRange.lowerBound):\(selectionLineRange.upperBound)"] + tool.arguments
    }

    static func processArguments(
        for tool: ExternalCodeFormatterTool,
        launch: ExternalCodeFormatterLaunch,
        selectionLineRange: ClosedRange<Int>?,
        sqlDialect: SQLFormatterDialect = .ansi
    ) -> [String] {
        let formatterArgumentCount = min(tool.arguments.count, launch.arguments.count)
        let launcherArguments = Array(launch.arguments.dropLast(formatterArgumentCount))
        return launcherArguments + processArguments(
            for: tool,
            selectionLineRange: selectionLineRange,
            sqlDialect: sqlDialect
        )
    }

    static func processArguments(
        for tool: ExternalCodeFormatterTool,
        launch: ExternalCodeFormatterLaunch,
        probeArguments: [String]
    ) -> [String] {
        let formatterArgumentCount = min(tool.arguments.count, launch.arguments.count)
        let launcherArguments = Array(launch.arguments.dropLast(formatterArgumentCount))
        return launcherArguments + probeArguments
    }

    static func probeArguments(for tool: ExternalCodeFormatterTool) -> [String] {
        switch tool.id {
        case "gofmt":
            return ["-h"]
        default:
            return ["--version"]
        }
    }

    static func availableLanguages(
        configuredPaths: [String: String],
        environment: [String: String] = ProcessInfo.processInfo.environment,
        fileManager: FileManager = .default,
        javaRuntimeLookup: ExternalCodeFormatterJavaRuntimeLookup = .automatic,
        perlModuleLookup: ExternalCodeFormatterPerlModuleLookup = .automatic
    ) -> [String] {
        var languages = Set<String>()
        for tool in tools {
            if case .available = availability(
            for: tool,
            configuredPaths: configuredPaths,
            environment: environment,
            fileManager: fileManager,
            javaRuntimeLookup: javaRuntimeLookup,
            perlModuleLookup: perlModuleLookup
            ) {
                languages.formUnion(tool.languages)
            }
        }
        return languages.sorted()
    }

    static func availability(
        for tool: ExternalCodeFormatterTool,
        configuredPaths: [String: String],
        environment: [String: String] = ProcessInfo.processInfo.environment,
        fileManager: FileManager = .default,
        javaRuntimeLookup: ExternalCodeFormatterJavaRuntimeLookup = .automatic,
        perlModuleLookup: ExternalCodeFormatterPerlModuleLookup = .automatic
    ) -> ExternalCodeFormatterAvailability {
        if tool.id == "latexindent" {
            let moduleIsAvailable: Bool = {
                switch perlModuleLookup {
                case .present:
                    return true
                case .missing:
                    return false
                case .automatic:
                    return perlModuleExists(
                        "File/HomeDir.pm",
                        environment: environment,
                        fileManager: fileManager
                    )
                }
            }()
            guard moduleIsAvailable else {
                return .latexMissingPerlRuntime
            }
        }

        if let configuredPath = configuredPaths[tool.id]?.trimmingCharacters(in: .whitespacesAndNewlines),
           !configuredPath.isEmpty {
            if configuredPath.lowercased().hasSuffix(".jar") {
                guard fileManager.fileExists(atPath: configuredPath) else {
                    return .invalidCustomPath
                }
                let javaPath = {
                    switch javaRuntimeLookup {
                    case .automatic:
                        return javaRuntimePath(environment: environment, fileManager: fileManager)
                    case .candidates(let candidates):
                        return candidates.first { fileManager.isExecutableFile(atPath: $0) }
                    }
                }()
                guard let javaPath else {
                    return .jarMissingJavaRuntime
                }
                return .available(ExternalCodeFormatterLaunch(
                    executablePath: javaPath,
                    arguments: ["-jar", configuredPath] + tool.arguments,
                    environment: runtimeEnvironment(for: tool)
                ))
            }

            if let executablePath = executablePath(configuredPath, environment: environment, fileManager: fileManager) {
                return .available(ExternalCodeFormatterLaunch(
                    executablePath: executablePath,
                    arguments: tool.arguments,
                    environment: runtimeEnvironment(for: tool)
                ))
            }
            return .invalidCustomPath
        }

        guard let executablePath = executablePath(tool.executableName, environment: environment, fileManager: fileManager) else {
            return .notInstalled
        }
        return .available(ExternalCodeFormatterLaunch(
            executablePath: executablePath,
            arguments: tool.arguments,
            environment: runtimeEnvironment(for: tool)
        ))
    }

    private static func runtimeEnvironment(for tool: ExternalCodeFormatterTool) -> [String: String] {
        var environment = ProcessInfo.processInfo.environment
        guard tool.id == "latexindent" else { return environment }

        // TeX Live's latexindent uses CPAN modules that are commonly installed
        // by local::lib into ~/perl5. GUI apps do not inherit a user's shell
        // PERL5LIB, so give the formatter the standard search locations here.
        let searchPaths = [
            NSHomeDirectory() + "/perl5/lib/perl5",
            "/opt/homebrew/lib/perl5/site_perl",
            "/usr/local/lib/perl5/site_perl",
        ]
        let inherited = environment["PERL5LIB"].map { $0.split(separator: ":").map(String.init) } ?? []
        environment["PERL5LIB"] = (searchPaths + inherited).joined(separator: ":")
        return environment
    }

    private static func perlModuleSearchPaths(environment: [String: String], fileManager: FileManager) -> [String] {
        var paths: [String] = []
        if let perl5Lib = environment["PERL5LIB"] {
            paths.append(contentsOf: perl5Lib.split(separator: ":").map(String.init))
        }
        paths.append(contentsOf: [
            NSHomeDirectory() + "/perl5/lib/perl5",
            "/opt/homebrew/lib/perl5/site_perl",
            "/usr/local/lib/perl5/site_perl",
        ])

        let systemRoots = [
            "/Library/Perl",
            "/Network/Library/Perl",
            "/System/Library/Perl",
        ]
        for root in systemRoots {
            let versions = (try? fileManager.contentsOfDirectory(atPath: root))?.sorted() ?? []
            for version in versions {
                paths.append("\(root)/\(version)/darwin-thread-multi-2level")
                paths.append("\(root)/\(version)")
            }
        }

        var uniquePaths: [String] = []
        var seen = Set<String>()
        for path in paths where !path.isEmpty && seen.insert(path).inserted {
            uniquePaths.append(path)
        }
        return uniquePaths
    }

    private static func perlModuleExists(
        _ modulePath: String,
        environment: [String: String],
        fileManager: FileManager
    ) -> Bool {
        perlModuleSearchPaths(environment: environment, fileManager: fileManager)
            .contains { fileManager.fileExists(atPath: "\($0)/\(modulePath)") }
    }

    static func launch(
        for tool: ExternalCodeFormatterTool,
        configuredPaths: [String: String],
        environment: [String: String] = ProcessInfo.processInfo.environment,
        fileManager: FileManager = .default,
        perlModuleLookup: ExternalCodeFormatterPerlModuleLookup = .automatic
    ) -> ExternalCodeFormatterLaunch? {
        if case .available(let launch) = availability(
            for: tool,
            configuredPaths: configuredPaths,
            environment: environment,
            fileManager: fileManager,
            perlModuleLookup: perlModuleLookup
        ) {
            return launch
        }
        return nil
    }

    static var javaRuntimeDownloadURL: URL {
        #if arch(arm64)
        return URL(string: "https://adoptium.net/temurin/releases/?os=mac&arch=aarch64&package=jdk")!
        #else
        return URL(string: "https://adoptium.net/temurin/releases/?os=mac&arch=x64&package=jdk")!
        #endif
    }

    static var perlModuleInstallURL: URL {
        URL(string: "https://metacpan.org/pod/File::HomeDir")!
    }

    private static func javaRuntimePath(
        environment: [String: String],
        fileManager: FileManager
    ) -> String? {
        for candidate in javaRuntimeCandidates(environment: environment) where fileManager.isExecutableFile(atPath: candidate) {
            return candidate
        }
        return nil
    }

    private static func javaRuntimeCandidates(environment: [String: String]) -> [String] {
        var candidates: [String] = []
        if let javaHome = environment["JAVA_HOME"]?.trimmingCharacters(in: .whitespacesAndNewlines),
           !javaHome.isEmpty {
            candidates.append(URL(fileURLWithPath: javaHome, isDirectory: true)
                .appendingPathComponent("bin")
                .appendingPathComponent("java").path)
        }

        candidates.append(contentsOf: [
            "/opt/homebrew/opt/openjdk/bin/java",
            "/usr/local/opt/openjdk/bin/java",
            "/opt/homebrew/bin/java",
            "/usr/local/bin/java",
        ])
        for prefix in ["/opt/homebrew/opt", "/usr/local/opt"] {
            let directory = URL(fileURLWithPath: prefix, isDirectory: true)
            let versions = (try? FileManager.default.contentsOfDirectory(atPath: directory.path))?.sorted() ?? []
            for version in versions where version.hasPrefix("openjdk@") {
                candidates.append(directory.appendingPathComponent(version)
                    .appendingPathComponent("bin")
                    .appendingPathComponent("java").path)
            }
        }

        for root in ["/Library/Java/JavaVirtualMachines", "\(NSHomeDirectory())/Library/Java/JavaVirtualMachines"] {
            let directory = URL(fileURLWithPath: root, isDirectory: true)
            let versions = (try? FileManager.default.contentsOfDirectory(atPath: directory.path))?.sorted() ?? []
            for version in versions {
                candidates.append(directory.appendingPathComponent(version)
                    .appendingPathComponent("Contents/Home/bin/java").path)
            }
        }

        let pathDirectories = environment["PATH"]?.split(separator: ":").map(String.init) ?? []
        for directory in pathDirectories {
            let candidate = URL(fileURLWithPath: directory, isDirectory: true)
                .appendingPathComponent("java").path
            // On macOS, /usr/bin/java can be an installation stub even when no JVM exists.
            if candidate != "/usr/bin/java" && candidate != "/bin/java" {
                candidates.append(candidate)
            }
        }
        return candidates
    }

    static func executablePath(
        _ name: String,
        environment: [String: String] = ProcessInfo.processInfo.environment,
        fileManager: FileManager = .default
    ) -> String? {
        if name.contains("/") {
            return fileManager.isExecutableFile(atPath: name) ? name : nil
        }

        let directories = environment["PATH"]?.split(separator: ":").map(String.init) ?? []
        for directory in directories {
            let candidate = URL(fileURLWithPath: directory, isDirectory: true)
                .appendingPathComponent(name).path
            if fileManager.isExecutableFile(atPath: candidate) {
                return candidate
            }
        }

        // GUI 应用不继承 shell 的 PATH，pip 装进 python.org 框架或用户目录的
        // 工具（如 black）必须按版本枚举这两个安装根。
        for root in ["/Library/Frameworks/Python.framework/Versions", "\(NSHomeDirectory())/Library/Python"] {
            guard let versions = try? fileManager.contentsOfDirectory(atPath: root) else { continue }
            // 版本目录按降序取最新；目录名形如 3.14 / 3.14-arm64。
            for version in versions.sorted(by: >) {
                let candidate = "\(root)/\(version)/bin/\(name)"
                if fileManager.isExecutableFile(atPath: candidate) {
                    return candidate
                }
            }
        }

        for candidate in fallbackExecutablePaths[name] ?? [] where fileManager.isExecutableFile(atPath: candidate) {
            return candidate
        }
        return nil
    }

    private static let fallbackExecutablePaths: [String: [String]] = [
        "black": [
            "/opt/homebrew/bin/black",
            "/usr/local/bin/black",
            "\(NSHomeDirectory())/.local/bin/black",
        ],
        "clang-format": [
            "/usr/bin/clang-format",
            "/Library/Developer/CommandLineTools/usr/bin/clang-format",
            "/Applications/Xcode.app/Contents/Developer/Toolchains/XcodeDefault.xctoolchain/usr/bin/clang-format",
            "/opt/homebrew/opt/clang-format/bin/clang-format",
            "/usr/local/opt/clang-format/bin/clang-format",
        ],
        "rustfmt": [
            "/opt/homebrew/bin/rustfmt",
            "/usr/local/bin/rustfmt",
            "\(NSHomeDirectory())/.cargo/bin/rustfmt",
        ],
        "gofmt": [
            "/opt/homebrew/bin/gofmt",
            "/usr/local/bin/gofmt",
            "/usr/local/go/bin/gofmt",
            "/opt/homebrew/opt/go/libexec/gofmt",
        ],
        "sqlfluff": [
            "/opt/homebrew/bin/sqlfluff",
            "/usr/local/bin/sqlfluff",
            "\(NSHomeDirectory())/.local/bin/sqlfluff",
        ],
        "verible-verilog-format": [
            "/opt/homebrew/bin/verible-verilog-format",
            "/opt/homebrew/opt/verible/bin/verible-verilog-format",
            "/usr/local/bin/verible-verilog-format",
            "/usr/local/opt/verible/bin/verible-verilog-format",
            "\(NSHomeDirectory())/.local/bin/verible-verilog-format",
        ],
        "shfmt": [
            "/opt/homebrew/bin/shfmt",
            "/usr/local/bin/shfmt",
            "\(NSHomeDirectory())/.local/bin/shfmt",
        ],
        "taplo": [
            "/opt/homebrew/bin/taplo",
            "/usr/local/bin/taplo",
            "\(NSHomeDirectory())/.local/bin/taplo",
        ],
        "xmllint": [
            "/usr/bin/xmllint",
            "/opt/homebrew/bin/xmllint",
            "/usr/local/bin/xmllint",
        ],
    ]
}
