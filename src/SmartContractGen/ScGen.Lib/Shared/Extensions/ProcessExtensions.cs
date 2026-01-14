namespace ScGen.Lib.Shared.Extensions;

public static class ProcessExtensions
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(15);
    private const string Docker = "docker";
    private const string Resim = "resim";

    public static async Task<ProcessExecutionResult> ExecuteAsync(
        this Process process,
        ILogger logger,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        TimeSpan actualTimeout = timeout ?? DefaultTimeout;
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(actualTimeout);

        try
        {
            DateTime startTime = DateTime.UtcNow;

            if (!process.Start())
            {
                return ProcessExecutionResult.Failure(
                    -1,
                    string.Empty,
                    Messages.FailedToStartProcess + process.StartInfo.FileName,
                    TimeSpan.Zero);
            }

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            string stdout = await stdoutTask;
            string stderr = await stderrTask;

            TimeSpan duration = DateTime.UtcNow - startTime;

            return new ProcessExecutionResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                IsSuccess = process.ExitCode == 0,
                Duration = duration,
                ProcessId = process.Id,
                StartTime = startTime
            };
        }
        catch (OperationCanceledException e) when (timeoutCts.Token.IsCancellationRequested &&
                                                   !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                logger.OperationFailedWithException(nameof(ExecuteAsync), ex.Message);
            }

            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                e.Message,
                actualTimeout);
        }
        catch (Exception ex)
        {
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                ex.Message,
                TimeSpan.Zero);
        }
    }


    public static ProcessExecutionResult Execute(
        this Process process,
        ILogger logger,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        TimeSpan actualTimeout = timeout ?? DefaultTimeout;
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(actualTimeout);

        try
        {
            DateTime startTime = DateTime.UtcNow;

            if (!process.Start())
            {
                return ProcessExecutionResult.Failure(
                    -1,
                    string.Empty,
                    Messages.FailedToStartProcess + process.StartInfo.FileName,
                    TimeSpan.Zero);
            }

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            process.WaitForExitAsync(timeoutCts.Token);


            TimeSpan duration = DateTime.UtcNow - startTime;

            return new ProcessExecutionResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                StandardError = stderr,
                IsSuccess = process.ExitCode == 0,
                Duration = duration,
                ProcessId = process.Id,
                StartTime = startTime
            };
        }
        catch (OperationCanceledException e) when (timeoutCts.Token.IsCancellationRequested &&
                                                   !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                logger.OperationFailedWithException(nameof(Execute), ex.Message);
            }

            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                e.Message,
                actualTimeout);
        }
        catch (Exception ex)
        {
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                ex.Message,
                TimeSpan.Zero);
        }
    }


    public static async Task<ProcessExecutionResult> RunCommandAsync(
        string fileName,
        string arguments,
        ILogger logger,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        using Process process = new();
        process.StartInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory);

        return await process.ExecuteAsync(logger, cancellationToken, timeout);
    }

    public static ProcessExecutionResult RunCommand(
        string fileName,
        string arguments,
        ILogger logger,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        using Process process = new();
        process.StartInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory);

        return process.Execute(logger, cancellationToken, timeout);
    }


    public static async Task<ProcessExecutionResult> RunCommandAsync(
        this Process process,
        string fileName,
        string arguments,
        ILogger logger,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null)
    {
        process.StartInfo = CreateProcessStartInfo(fileName, arguments, workingDirectory);
        return await process.ExecuteAsync(logger, cancellationToken, timeout);
    }

    public static async Task<ProcessExecutionResult> RunSolcAsync(
        string sourceFilePath,
        string outputDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string solc = "solc";
        if (!File.Exists(sourceFilePath))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.FileNotFound,
                TimeSpan.Zero);

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        string arguments = new StringBuilder()
            .Append("--abi --bin --optimize ")
            .Append($"-o \"{outputDirectory}\" ")
            .Append($"\"{sourceFilePath}\"")
            .ToString();

        return await RunCommandAsync(solc, arguments, logger, outputDirectory, cancellationToken, TimeSpan.FromMinutes(2));
    }

    public static async Task<ProcessExecutionResult> RunCargoAsync(
        string command,
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string cargo = "cargo";

        if (!Directory.Exists(workingDirectory))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.WorkingDirectoryNotFound,
                TimeSpan.Zero);

        return await RunCommandAsync(cargo, command, logger, workingDirectory, cancellationToken, TimeSpan.FromMinutes(10));
    }

    public static async Task<ProcessExecutionResult> RunCargoAsyncWithEnv(
        string command,
        string workingDirectory,
        ILogger logger,
        Dictionary<string, string> environmentVariables,
        CancellationToken cancellationToken = default)
    {
        const string cargo = "cargo";

        if (!Directory.Exists(workingDirectory))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.WorkingDirectoryNotFound,
                TimeSpan.Zero);

        using Process process = new();
        process.StartInfo = CreateProcessStartInfo(cargo, command, workingDirectory);
        
        // Add environment variables
        foreach (var envVar in environmentVariables)
        {
            process.StartInfo.Environment[envVar.Key] = envVar.Value;
        }

        return await process.ExecuteAsync(logger, cancellationToken, TimeSpan.FromMinutes(10));
    }


    public static async Task<ProcessExecutionResult> RunAnchorAsync(
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string anchor = "anchor";
        // Note: --locked flag is not supported by cargo-build-sbf (used by Anchor)
        // Instead, we rely on comprehensive Cargo.lock fixes in Step 5 before anchor build
        // These fixes remove constant_time_eq 0.4.2 and fix all references
        const string arguments = " build";
        if (!Directory.Exists(workingDirectory))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.WorkingDirectoryNotFound,
                TimeSpan.Zero);

        // Use isolated CARGO_HOME per compilation (similar to OASIS's versioned dependency paths)
        // This prevents dependency conflicts between different compilations
        string isolatedCargoHome = Path.Combine(workingDirectory, ".cargo_home");
        Directory.CreateDirectory(isolatedCargoHome);
        
        using Process process = new();
        process.StartInfo = CreateProcessStartInfo(anchor, arguments, workingDirectory);
        // Use isolated CARGO_HOME to prevent cross-contamination
        process.StartInfo.Environment["CARGO_HOME"] = isolatedCargoHome;
        logger.LogInformation("Using isolated CARGO_HOME: {CargoHome}", isolatedCargoHome);
        
        // Ensure Solana CLI tools are in PATH for cargo-build-sbf
        // Try Agave path first (newer), then fall back to old solana-install path
        string solanaBinPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "solana", "install", "active_release", "bin");
        
        // Also check for platform-tools SDK and create symlink if needed
        string platformToolsPath = Path.Combine(solanaBinPath, "..", "platform-tools");
        string cargoBinPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cargo", "bin");
        string platformToolsSymlink = Path.Combine(cargoBinPath, "platform-tools-sdk");
        
        // Create symlink for platform-tools if it doesn't exist and platform-tools directory exists
        if (Directory.Exists(platformToolsPath) && !Directory.Exists(platformToolsSymlink))
        {
            try
            {
                Directory.CreateDirectory(cargoBinPath);
                // Create symlink (Unix/Mac) or junction (Windows)
                if (Environment.OSVersion.Platform == PlatformID.Unix || 
                    Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    var symlinkProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "ln",
                        Arguments = $"-s \"{platformToolsPath}\" \"{platformToolsSymlink}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    symlinkProcess?.WaitForExit(1000);
                    logger.LogInformation("Created symlink for platform-tools-sdk: {Symlink} -> {Target}", platformToolsSymlink, platformToolsPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create platform-tools-sdk symlink: {Error}", ex.Message);
            }
        }
        
        // Add cargo wrapper to PATH (if it exists) to handle Cargo.lock version 4
        string cargoWrapperPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cargo", "bin");
        
        // Check for local cargo wrapper in temp directory (fixes constant_time_eq 0.4.2)
        string localCargoWrapperDir = Path.Combine(workingDirectory, ".cargo_wrapper");
        string localCargoWrapper = Path.Combine(localCargoWrapperDir, "cargo");
        
        string currentPath = process.StartInfo.Environment["PATH"] ?? string.Empty;
        var pathParts = currentPath.Split(':', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        // CRITICAL: Put local cargo wrapper FIRST (highest priority) - it fixes constant_time_eq 0.4.2
        if (File.Exists(localCargoWrapper))
        {
            pathParts.RemoveAll(p => p == localCargoWrapperDir);
            pathParts.Insert(0, localCargoWrapperDir);
            logger.LogInformation("Prioritized local cargo wrapper in PATH (fixes constant_time_eq 0.4.2): {WrapperDir}", localCargoWrapperDir);
        }
        // Then stable cargo (if local wrapper doesn't exist)
        else if (Directory.Exists(cargoWrapperPath))
        {
            // Remove any existing cargo path entries
            pathParts.RemoveAll(p => p.Contains(".cargo/bin"));
            // Add stable cargo FIRST (highest priority)
            pathParts.Insert(0, cargoWrapperPath);
            logger.LogInformation("Prioritized stable cargo in PATH: {CargoWrapperPath}", cargoWrapperPath);
        }
        
        // Add Solana CLI tools AFTER stable cargo (lower priority)
        // This way stable cargo is found first, but Solana tools are still available
        if (Directory.Exists(solanaBinPath))
        {
            // Remove Solana bin if already present
            pathParts.RemoveAll(p => p == solanaBinPath);
            // Add Solana tools AFTER stable cargo
            pathParts.Add(solanaBinPath);
            logger.LogInformation("Added Solana CLI to PATH (after stable cargo): {SolanaBinPath}", solanaBinPath);
        }
        else
        {
            logger.LogWarning("Solana CLI bin directory not found at: {SolanaBinPath}", solanaBinPath);
        }
        
        process.StartInfo.Environment["PATH"] = string.Join(":", pathParts);
        
        // Force Anchor to use stable Rust toolchain instead of Solana toolchain
        // This ensures we use Cargo 1.92.0 instead of 1.75.0 from Solana toolchain
        // Set RUSTUP_TOOLCHAIN to force stable toolchain
        process.StartInfo.Environment["RUSTUP_TOOLCHAIN"] = "stable";
        logger.LogInformation("Set RUSTUP_TOOLCHAIN=stable to force stable Rust toolchain (Cargo 1.92.0)");
        
        // CRITICAL: Set CARGO to use our wrapper if it exists (fixes constant_time_eq 0.4.2)
        // This ensures cargo calls go through our wrapper even if CARGO env var is checked
        if (File.Exists(localCargoWrapper))
        {
            process.StartInfo.Environment["CARGO"] = localCargoWrapper;
            logger.LogInformation("Set CARGO={WrapperPath} to use cargo wrapper (fixes constant_time_eq 0.4.2)", localCargoWrapper);
        }
        // Otherwise, set CARGO to explicitly use stable cargo if available
        else
        {
            string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string stableCargoPath = Path.Combine(homeDir, ".cargo", "bin", "cargo");
            if (File.Exists(stableCargoPath))
            {
                process.StartInfo.Environment["CARGO"] = stableCargoPath;
                logger.LogInformation("Set CARGO={CargoPath} to use stable cargo", stableCargoPath);
            }
        }
        
        // Use sccache if available for shared compilation cache
        string sccachePath = FindSccache();
        if (!string.IsNullOrEmpty(sccachePath))
        {
            process.StartInfo.Environment["RUSTC_WRAPPER"] = sccachePath;
            // sccache doesn't work with CARGO_INCREMENTAL, so don't set it
            logger.LogInformation("Using sccache for faster compilation: {SccachePath}", sccachePath);
        }
        else
        {
            // Only use incremental compilation if sccache is not available
            process.StartInfo.Environment["CARGO_INCREMENTAL"] = "1";
            logger.LogWarning("sccache not found - compilation will be slower. Install with: cargo install sccache");
        }
        
        // First builds can take 20+ minutes due to dependency downloads
        // Anchor build runs multiple phases (program + tests/IDL) which can take time
        // Increase timeout to 45 minutes to handle file lock contention on cargo cache
        return await process.ExecuteAsync(logger, cancellationToken, TimeSpan.FromMinutes(45));
    }
    
    private static string FindSccache()
    {
        // Try common locations
        string[] possiblePaths = 
        {
            "/usr/local/bin/sccache",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo/bin/sccache")
        };
        
        foreach (string path in possiblePaths)
        {
            if (File.Exists(path))
                return path;
        }
        
        // Try which command
        try
        {
            using Process process = new();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = "sccache",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            process.Start();
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            
            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                return output;
        }
        catch
        {
            // Ignore
        }
        
        return string.Empty;
    }

    public static async Task<ProcessExecutionResult> RunScryptoAsync(
        string workingDirectory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        const string scrypto = "scrypto";
        const string arguments = " build";
        if (!Directory.Exists(workingDirectory))
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                Messages.WorkingDirectoryNotFound,
                TimeSpan.Zero);

        return await RunCommandAsync(scrypto, arguments, logger, workingDirectory, cancellationToken, TimeSpan.FromMinutes(5));
    }


    private static Process? GanacheProcess { get; set; }

    public static ProcessExecutionResult RunGanache(
        ILogger logger,
        int port = 8545,
        string? workingDirectory = null)
    {
        const string shell = "SHELL";
        const string bash = "/bin/bash";
        string file = Environment.GetEnvironmentVariable(shell) ?? bash;

        try
        {
            StringBuilder arguments = new StringBuilder().Append("-c \"ganache --deterministic\"");
            if (port is > 0 and <= 65535 && port != 8545)
                arguments.Append($" --port {port}");

            DateTime startTime = DateTime.UtcNow;

            Process process = new()
            {
                StartInfo = new()
                {
                    FileName = file,
                    Arguments = arguments.ToString(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    logger.LogInformation("[Ganache STDOUT] {msg}", e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    logger.LogError("[Ganache STDERR] {msg}", e.Data);
            };

            if (!process.Start())
            {
                return ProcessExecutionResult.Failure(
                    -1,
                    string.Empty,
                    Messages.FailedToStartProcess + process.StartInfo.FileName,
                    TimeSpan.Zero);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            GanacheProcess = process;

            return new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "Ganache started",
                StandardError = string.Empty,
                IsSuccess = true,
                Duration = TimeSpan.Zero,
                ProcessId = process.Id,
                StartTime = startTime
            };
        }
        catch (Exception e)
        {
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                e.Message,
                TimeSpan.Zero);
        }
    }

    public static void StopGanache(ILogger logger)
    {
        try
        {
            if (GanacheProcess is { HasExited: false })
            {
                GanacheProcess.Kill(entireProcessTree: true);
                logger.LogInformation("Ganache stopped.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop Ganache.");
        }
        finally
        {
            GanacheProcess = null;
        }
    }

    private static ProcessStartInfo CreateProcessStartInfo(
        string fileName,
        string arguments,
        string? workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
    }


    private static Process? SolanaTestValidatorProcess { get; set; }

    public static ProcessExecutionResult RunSolanaTestValidator(
        ILogger logger,
        int port = 8545,
        string? workingDirectory = null)
    {
        const string file = "solana-test-validator";
        try
        {
            string arguments = string.Empty;
            if (port is > 0 and <= 65535 && port != 8545)
                arguments = $" --rpc-port {port}";

            DateTime startTime = DateTime.UtcNow;

            Process process = new()
            {
                StartInfo = new()
                {
                    FileName = file,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    logger.LogInformation("[SOLANA STDOUT] {msg}", e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    logger.LogError("[SOLANA STDERR] {msg}", e.Data);
            };

            if (!process.Start())
            {
                return ProcessExecutionResult.Failure(
                    -1,
                    string.Empty,
                    Messages.FailedToStartProcess + process.StartInfo.FileName,
                    TimeSpan.Zero);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            SolanaTestValidatorProcess = process;

            return new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "Solana test validator started",
                StandardError = string.Empty,
                IsSuccess = true,
                Duration = TimeSpan.Zero,
                ProcessId = process.Id,
                StartTime = startTime
            };
        }
        catch (Exception e)
        {
            return ProcessExecutionResult.Failure(
                -1,
                string.Empty,
                e.Message,
                TimeSpan.Zero);
        }
    }

    public static void StopSolanaTestValidator(ILogger logger)
    {
        try
        {
            if (SolanaTestValidatorProcess is { HasExited: false })
            {
                SolanaTestValidatorProcess.Kill(entireProcessTree: true);
                logger.LogInformation("Ganache stopped.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop Ganache.");
        }
        finally
        {
            SolanaTestValidatorProcess = null;
        }
    }

    public static ProcessExecutionResult CheckDockerImage(
        string imageName,
        ILogger logger)
    {
        string arguments = $"image inspect {imageName}";
        return RunCommand(Docker, arguments, logger);
    }

    public static ProcessExecutionResult PullDockerImage(
        string imageName,
        ILogger logger)
    {
        string arguments = $"pull {imageName}";
        return RunCommand(Docker, arguments, logger);
    }

    public static ProcessExecutionResult CheckDockerAvailability(
        ILogger logger)
    {
        string arguments = "--version";
        return RunCommand(Docker, arguments, logger);
    }

    public static void CleanupDockerContainer(
        string containerName,
        ILogger logger)
    {
        string arguments = $"rm -f {containerName}";
        RunCommand(Docker, arguments, logger);
    }


    private static string GetCurrentUserId(ILogger logger)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "1000";

        const string file = "id";
        const string arguments = "-u";

        return RunCommand(file, arguments, logger).StandardOutput.Trim();
    }

    private static string GetCurrentGroupId(ILogger logger)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "1000";

        const string file = "id";
        const string arguments = "-g";

        return RunCommand(file, arguments, logger).StandardOutput.Trim();
    }

    public static ProcessExecutionResult RunDockerContainerAsCurrentUser(
        string imageName, string containerName,
        string hostPath, string templateName, ILogger logger)
    {
        string uid = GetCurrentUserId(logger);
        string gid = GetCurrentGroupId(logger);
        string arguments =
            $"run --name {containerName} --rm --user {uid}:{gid} -v \"{hostPath}:/workspace\" -w /workspace {imageName} scrypto new-package {templateName}";
        return RunCommand(Docker, arguments, logger);
    }

    public static ProcessExecutionResult RunScryptoCompiler(
        string containerName, string projectDir, string cargoCache,
        string dockerImageName, string uid, string gid, ILogger logger)
    {
        string arguments =
            $"run --name {containerName} --rm " +
            $"--user {uid}:{gid} " +
            $"-v \"{projectDir}:/workspace\" " +
            $"-v \"{cargoCache}:/usr/local/cargo/registry\" " +
            $"-w /workspace " +
            $"-e CARGO_HOME=/usr/local/cargo " +
            $"-e USER_ID={uid} " +
            $"-e GROUP_ID={gid} " +
            $"{dockerImageName} " +
            $"sh -c \"" +
            $"mkdir -p /usr/local/cargo/registry && " +
            $"chmod -R 777 /usr/local/cargo/registry && " +
            $"scrypto build\"";

        return RunCommand(Docker, arguments, logger);
    }

    public static ProcessExecutionResult RunScryptoDeployer(string containerName,
        string uid, string gid, string workingDir, string homeDir, ILogger logger)
    {
        string arguments = $"run --name {containerName} --rm " +
                           $"--user {uid}:{gid} " +
                           $"-v \"{workingDir}:/workspace\" " +
                           $"-v \"{homeDir}:/home/runner\" " +
                           $"-w /workspace " +
                           $"-e HOME=/home/runner " +
                           $"-e USER_ID={uid} " +
                           $"-e GROUP_ID={gid} " +
                           $"{DockerImages.ScryptoToolsImage} " +
                           $"sh -c \"chmod +x deploy.sh && ./deploy.sh\"";
        return RunCommand(Docker, arguments, logger);
    }

    public static ProcessExecutionResult EnsureDefaultResimAccountExists(ILogger logger)
    {
        string arguments = "show";
        return RunCommand(Resim, arguments, logger);
    }

    public static ProcessExecutionResult CreateDefaultResimAccount(ILogger logger)
    {
        string arguments = "new-account";
        return RunCommand(Resim, arguments, logger);
    }

    public static async Task<ProcessExecutionResult> DeployRadixContractAsync(string wasmFilePath, ILogger logger)
    {
        string arguments = $"publish {wasmFilePath}";
        return await RunCommandAsync(Resim, arguments, logger);
    }
}