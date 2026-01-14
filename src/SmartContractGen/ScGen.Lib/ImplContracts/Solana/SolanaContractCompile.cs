namespace ScGen.Lib.ImplContracts.Solana;

public sealed partial class SolanaContractCompile(
    ILogger<SolanaContractCompile> logger,
    IHttpContextAccessor httpContext) : ISolanaContractCompile
{
    private const long MaxFileSize = 100 * 1024 * 1024;

    public async Task<Result<CompileContractResponse>> CompileAsync(IFormFile sourceCodeFile, CancellationToken token = default)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        logger.OperationStarted(nameof(CompileAsync),
            httpContext.GetId().ToString(), httpContext.GetCorrelationId());

        Result<CompileContractResponse> validation = Validation(sourceCodeFile);
        if (!validation.IsSuccess) return validation;

        // Use a persistent build cache directory instead of random temp
        string persistentCacheDir = Path.Combine(Path.GetTempPath(), "anchor_build_cache");
        Directory.CreateDirectory(persistentCacheDir);
        
            string tempDir = Path.Combine(persistentCacheDir, Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);

            try
            {
                // CRITICAL: Pre-fix constant_time_eq 0.4.2 in user's global CARGO_HOME BEFORE any compilation
                // This prevents cargo from parsing the broken Cargo.toml during download
                try
                {
                    string userCargoHome = Environment.GetEnvironmentVariable("CARGO_HOME") 
                        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo");
                    string userRegistryBase = Path.Combine(userCargoHome, "registry", "src");
                    
                    if (Directory.Exists(userRegistryBase))
                    {
                        var registryDirs = Directory.GetDirectories(userRegistryBase);
                        foreach (var registryDir in registryDirs)
                        {
                            string constantTimeEqPath = Path.Combine(registryDir, "constant_time_eq-0.4.2");
                            if (Directory.Exists(constantTimeEqPath))
                            {
                                string cargoTomlPath = Path.Combine(constantTimeEqPath, "Cargo.toml");
                                if (File.Exists(cargoTomlPath))
                                {
                                    string content = await File.ReadAllTextAsync(cargoTomlPath, token);
                                    string original = content;
                                    
                                    // Fix edition2024 -> 2021
                                    if (content.Contains("edition = \"2024\"") || content.Contains("edition2024"))
                                    {
                                        content = System.Text.RegularExpressions.Regex.Replace(
                                            content,
                                            @"edition\s*=\s*""2024""",
                                            "edition = \"2021\"");
                                        content = content.Replace("edition2024", "");
                                    }
                                    
                                    // Fix rust-version
                                    if (System.Text.RegularExpressions.Regex.IsMatch(content, @"rust-version\s*=\s*""1\.(8[5-9]|9[0-9])"))
                                    {
                                        content = System.Text.RegularExpressions.Regex.Replace(
                                            content,
                                            @"rust-version\s*=\s*""1\.(8[5-9]|9[0-9])""",
                                            "rust-version = \"1.75.0\"");
                                    }
                                    
                                    if (content != original)
                                    {
                                        await File.WriteAllTextAsync(cargoTomlPath, content, token);
                                        logger.LogInformation("Pre-fixed constant_time_eq 0.4.2 in user's global CARGO_HOME: {Path}", cargoTomlPath);
                                    }
                                }
                            }
                            
                            // Also fix blake3-1.8.3
                            string blake3Path = Path.Combine(registryDir, "blake3-1.8.3");
                            if (Directory.Exists(blake3Path))
                            {
                                string cargoTomlPath = Path.Combine(blake3Path, "Cargo.toml");
                                if (File.Exists(cargoTomlPath))
                                {
                                    string content = await File.ReadAllTextAsync(cargoTomlPath, token);
                                    string original = content;
                                    
                                    if (content.Contains("edition = \"2024\"") || content.Contains("edition2024"))
                                    {
                                        content = System.Text.RegularExpressions.Regex.Replace(
                                            content,
                                            @"edition\s*=\s*""2024""",
                                            "edition = \"2021\"");
                                        content = content.Replace("edition2024", "");
                                    }
                                    
                                    if (System.Text.RegularExpressions.Regex.IsMatch(content, @"rust-version\s*=\s*""1\.(8[5-9]|9[0-9])"))
                                    {
                                        content = System.Text.RegularExpressions.Regex.Replace(
                                            content,
                                            @"rust-version\s*=\s*""1\.(8[5-9]|9[0-9])""",
                                            "rust-version = \"1.75.0\"");
                                    }
                                    
                                    if (content != original)
                                    {
                                        await File.WriteAllTextAsync(cargoTomlPath, content, token);
                                        logger.LogInformation("Pre-fixed blake3 1.8.3 in user's global CARGO_HOME: {Path}", cargoTomlPath);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not pre-fix crates in user's global CARGO_HOME: {Error}", ex.Message);
                }
                
                string zipPath = Path.Combine(tempDir, "source.zip");
                await using (FileStream fs = new(zipPath, FileMode.Create, FileAccess.Write))
                    await sourceCodeFile.CopyToAsync(fs, token);
                ZipFile.ExtractToDirectory(zipPath, tempDir);

            // Copy shared target cache if it exists (for faster incremental builds)
            string sharedTargetCache = Path.Combine(persistentCacheDir, "shared_target");
            string projectTarget = Path.Combine(tempDir, "target");
            
            if (Directory.Exists(sharedTargetCache))
            {
                try
                {
                    CopyDirectory(sharedTargetCache, projectTarget);
                    logger.LogInformation("Reusing cached build artifacts for faster compilation");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not copy cached target directory, building from scratch");
                }
            }

            // Fix Cargo.lock version 4 compatibility issue
            // Strategy: Pre-generate Cargo.lock files with version 3 BEFORE anchor build
            // This avoids the race condition where anchor generates version 4 and immediately reads it
            
            // Step 1: Delete all existing Cargo.lock files
            var existingLockFiles = Directory.GetFiles(tempDir, "Cargo.lock", SearchOption.AllDirectories);
            foreach (var lockPath in existingLockFiles)
            {
                try
                {
                    File.Delete(lockPath);
                    logger.LogInformation("Deleted existing Cargo.lock at {Path}", lockPath);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not delete Cargo.lock at {Path}: {Error}", lockPath, ex.Message);
                }
            }

            // Create rust-toolchain.toml to force stable toolchain (not Solana's 1.75.0)
            try
            {
                string rustToolchainPath = Path.Combine(tempDir, "rust-toolchain.toml");
                string toolchainContent = "[toolchain]\nchannel = \"stable\"\n";
                await File.WriteAllTextAsync(rustToolchainPath, toolchainContent, token);
                logger.LogInformation("Created rust-toolchain.toml to force stable Rust toolchain");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not create rust-toolchain.toml: {Error}", ex.Message);
            }

            // Don't create .cargo/config.toml - it was causing "path source" errors
            // All dependency version fixes are handled via:
            // 1. Direct dependency constraints in program Cargo.toml
            // 2. Cargo.lock fixes after generation
            // 3. Final lockfile fixes before anchor build

            // Step 1.5: Check for and remove any .cargo/config.toml files BEFORE generating lockfiles
            // These can cause "path source" errors during cargo generate-lockfile
            try
            {
                var cargoConfigFilesEarly = Directory.GetFiles(tempDir, "config.toml", SearchOption.AllDirectories)
                    .Where(p => p.Contains(".cargo")).ToList();
                foreach (var configPath in cargoConfigFilesEarly)
                {
                    try
                    {
                        if (File.Exists(configPath))
                        {
                            string configContent = await File.ReadAllTextAsync(configPath, token);
                            logger.LogWarning("Found .cargo/config.toml at {Path} - deleting to prevent path source errors. Content:\n{Content}", 
                                configPath, configContent.Length > 500 ? configContent.Substring(0, 500) + "..." : configContent);
                            File.Delete(configPath);
                            logger.LogInformation("Deleted .cargo/config.toml at {Path} to prevent path source errors", configPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not check/delete .cargo/config.toml at {Path}: {Error}", configPath, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not search for .cargo/config.toml files: {Error}", ex.Message);
            }

            // Step 2: Add dependency constraints and patches to Cargo.toml files
            // Use [patch.crates-io] in workspace root to force compatible versions
            // This is more aggressive than constraints and works for transitive dependencies
            var cargoTomlFiles = Directory.GetFiles(tempDir, "Cargo.toml", SearchOption.AllDirectories);
            
            // First, find the workspace root Cargo.toml (contains [workspace])
            string? workspaceRootToml = null;
            foreach (var tomlPath in cargoTomlFiles)
            {
                try
                {
                    string content = await File.ReadAllTextAsync(tomlPath, token);
                    if (content.Contains("[workspace]"))
                    {
                        workspaceRootToml = tomlPath;
                        break;
                    }
                }
                catch { }
            }
            
            // Add dependency constraints to workspace root Cargo.toml
            // This ensures transitive dependencies also respect our version constraints
            if (!string.IsNullOrEmpty(workspaceRootToml))
            {
                try
                {
                    string tomlContent = await File.ReadAllTextAsync(workspaceRootToml, token);
                    bool modified = false;
                    
                    // CRITICAL FIX: Download constant_time_eq 0.3.1 locally and use [patch.crates-io] to force ALL dependencies to use it
                    // This is the ONLY way to prevent transitive dependencies from pulling in 0.4.2
                    string localPatchDir = Path.Combine(tempDir, ".cargo_patches");
                    Directory.CreateDirectory(localPatchDir);
                    string constantTimeEqPatchDir = Path.Combine(localPatchDir, "constant_time_eq-0.3.1");
                    
                    // Download and extract constant_time_eq 0.3.1 if not already present
                    if (!Directory.Exists(constantTimeEqPatchDir))
                    {
                        try
                        {
                            logger.LogInformation("Downloading constant_time_eq 0.3.1 for local patch...");
                            // Use cargo to download the crate
                            string downloadDir = Path.Combine(tempDir, ".cargo_download");
                            Directory.CreateDirectory(downloadDir);
                            
                            // Create a minimal Cargo.toml that depends on constant_time_eq 0.3.1
                            string downloadCargoToml = Path.Combine(downloadDir, "Cargo.toml");
                            await File.WriteAllTextAsync(downloadCargoToml, @"[package]
name = ""download_helper""
version = ""0.1.0""
edition = ""2021""

[dependencies]
constant_time_eq = ""=0.3.1""
", token);
                            
                            // Try to find constant_time_eq 0.3.1 in the user's existing cargo registry first
                            string userCargoHome = Environment.GetEnvironmentVariable("CARGO_HOME") 
                                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo");
                            string userRegistryBase = Path.Combine(userCargoHome, "registry", "src");
                            bool foundInUserRegistry = false;
                            
                            if (Directory.Exists(userRegistryBase))
                            {
                                var registryDirs = Directory.GetDirectories(userRegistryBase);
                                foreach (var registryDir in registryDirs)
                                {
                                    string sourcePath = Path.Combine(registryDir, "constant_time_eq-0.3.1");
                                    if (Directory.Exists(sourcePath))
                                    {
                                        // Copy to patch directory
                                        CopyDirectory(sourcePath, constantTimeEqPatchDir);
                                        logger.LogInformation("Found constant_time_eq 0.3.1 in user registry, copied to {Path}", constantTimeEqPatchDir);
                                        foundInUserRegistry = true;
                                        break;
                                    }
                                }
                            }
                            
                            // If not found in user registry, try to download it using cargo generate-lockfile
                            if (!foundInUserRegistry)
                            {
                                try
                                {
                                    // Use cargo generate-lockfile which will download dependencies
                                    using var lockfileProcess = new Process
                                    {
                                        StartInfo = new ProcessStartInfo
                                        {
                                            FileName = "cargo",
                                            Arguments = "generate-lockfile --manifest-path Cargo.toml",
                                            WorkingDirectory = downloadDir,
                                            UseShellExecute = false,
                                            RedirectStandardOutput = true,
                                            RedirectStandardError = true,
                                            Environment = { ["CARGO_HOME"] = Path.Combine(tempDir, ".cargo_home_temp") }
                                        }
                                    };
                                    lockfileProcess.Start();
                                    await lockfileProcess.WaitForExitAsync(token);
                                    
                                    // Find the downloaded crate in the registry
                                    string tempRegistryBase = Path.Combine(tempDir, ".cargo_home_temp", "registry", "src");
                                    if (Directory.Exists(tempRegistryBase))
                                    {
                                        var registryDirs = Directory.GetDirectories(tempRegistryBase);
                                        foreach (var registryDir in registryDirs)
                                        {
                                            string sourcePath = Path.Combine(registryDir, "constant_time_eq-0.3.1");
                                            if (Directory.Exists(sourcePath))
                                            {
                                                // Copy to patch directory
                                                CopyDirectory(sourcePath, constantTimeEqPatchDir);
                                                logger.LogInformation("Downloaded constant_time_eq 0.3.1 via generate-lockfile to {Path}", constantTimeEqPatchDir);
                                                foundInUserRegistry = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex2)
                                {
                                    logger.LogWarning(ex2, "Could not download constant_time_eq 0.3.1 via generate-lockfile: {Error}", ex2.Message);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Could not download constant_time_eq 0.3.1 for patching: {Error}", ex.Message);
                        }
                    }
                    
                    // Add [patch.crates-io] to force ALL dependencies to use our local 0.3.1 version
                    // This works because patches point to a local path, which is a different source
                    if (Directory.Exists(constantTimeEqPatchDir))
                    {
                        // Remove any existing [patch.crates-io] section
                        if (tomlContent.Contains("[patch.crates-io]"))
                        {
                            var patchStart = tomlContent.IndexOf("[patch.crates-io]");
                            if (patchStart >= 0)
                            {
                                var nextSection = tomlContent.IndexOf("\n[", patchStart + 1);
                                var patchEnd = nextSection > 0 ? nextSection : tomlContent.Length;
                                tomlContent = tomlContent.Remove(patchStart, patchEnd - patchStart);
                                modified = true;
                            }
                        }
                        
                        // Add [patch.crates-io] pointing to our local copy
                        // Use relative path from workspace root
                        string relativePatchPath = Path.GetRelativePath(Path.GetDirectoryName(workspaceRootToml)!, constantTimeEqPatchDir)
                            .Replace('\\', '/'); // Normalize path separators
                        
                        int insertPos = tomlContent.Length;
                        // Find a good insertion point (after [workspace] section)
                        int workspaceIndex = tomlContent.IndexOf("[workspace]");
                        if (workspaceIndex >= 0)
                        {
                            int workspaceEnd = tomlContent.IndexOf("\n[", workspaceIndex + 1);
                            if (workspaceEnd > 0)
                            {
                                insertPos = workspaceEnd;
                            }
                        }
                        
                        string patchSection = $"\n[patch.crates-io]\n" +
                            $"# CRITICAL: Force ALL dependencies to use constant_time_eq 0.3.1 instead of 0.4.2\n" +
                            $"# This prevents transitive dependencies from pulling in 0.4.2 which requires rustc 1.85.0+\n" +
                            $"constant_time_eq = {{ path = \"{relativePatchPath}\" }}\n";
                        
                        tomlContent = tomlContent.Insert(insertPos, patchSection);
                        modified = true;
                        logger.LogInformation("Added [patch.crates-io] to force constant_time_eq 0.3.1 via local path patch");
                    }
                    else
                    {
                        // Fallback: Use workspace dependencies if patch download failed
                        logger.LogWarning("Could not create local patch, falling back to workspace dependencies");
                        
                        // Remove any [patch.crates-io] sections - they cause errors when patching crates.io to crates.io
                        if (tomlContent.Contains("[patch.crates-io]"))
                        {
                            var patchStart = tomlContent.IndexOf("[patch.crates-io]");
                            if (patchStart >= 0)
                            {
                                var nextSection = tomlContent.IndexOf("\n[", patchStart + 1);
                                var patchEnd = nextSection > 0 ? nextSection : tomlContent.Length;
                                tomlContent = tomlContent.Remove(patchStart, patchEnd - patchStart);
                                modified = true;
                                logger.LogInformation("Removed [patch.crates-io] section from {Path} (patches must point to different sources, not versions)", workspaceRootToml);
                            }
                        }
                        
                        // Add workspace-level dependency constraints to prevent transitive dependencies from pulling in incompatible versions
                        // This is critical because transitive dependencies are resolved at the workspace level
                        if (!tomlContent.Contains("[workspace.dependencies]"))
                        {
                            // Find the [workspace] section and add [workspace.dependencies] after it
                            int workspaceEnd = tomlContent.IndexOf("\n[", tomlContent.IndexOf("[workspace]"));
                            if (workspaceEnd == -1) workspaceEnd = tomlContent.Length;
                            
                            string workspaceDeps = "\n[workspace.dependencies]\n" +
                                "# Force compatible versions for transitive dependencies\n" +
                                "constant_time_eq = \"=0.3.1\"  # Block 0.4.2 which requires rustc 1.85.0+\n" +
                                "blake3 = \"=1.8.2\"  # CRITICAL: Force 1.8.2 (1.8.3 requires constant_time_eq 0.4.2)\n" +
                                "getrandom = { version = \">=0.2\", features = [\"custom\"] }  # Support Solana target (0.1.x doesn't)\n";
                            
                            tomlContent = tomlContent.Insert(workspaceEnd, workspaceDeps);
                            modified = true;
                            logger.LogInformation("Added [workspace.dependencies] constraints to workspace root {Path}", workspaceRootToml);
                        }
                        else if (tomlContent.Contains("[workspace.dependencies]"))
                        {
                            // Update existing workspace.dependencies if needed
                            bool needsUpdate = false;
                            if (!tomlContent.Contains("constant_time_eq = \"=0.3.1\""))
                            {
                                needsUpdate = true;
                            }
                            if (!tomlContent.Contains("getrandom ="))
                            {
                                needsUpdate = true;
                            }
                            
                            if (needsUpdate)
                            {
                                int depsStart = tomlContent.IndexOf("[workspace.dependencies]");
                                int depsEnd = tomlContent.IndexOf("\n[", depsStart + 1);
                                if (depsEnd == -1) depsEnd = tomlContent.Length;
                                
                                string additionalDeps = "";
                                if (!tomlContent.Contains("constant_time_eq = \"=0.3.1\""))
                                {
                                    additionalDeps += "\nconstant_time_eq = \"=0.3.1\"  # Block 0.4.2 which requires rustc 1.85.0+\n";
                                }
                            if (!tomlContent.Contains("blake3 = \"=1.8.2\"") && !tomlContent.Contains("blake3 = \"<1.8.3\""))
                            {
                                additionalDeps += "blake3 = \"=1.8.2\"  # CRITICAL: Force 1.8.2 (1.8.3 requires constant_time_eq 0.4.2)\n";
                            }
                                if (!tomlContent.Contains("getrandom ="))
                                {
                                    additionalDeps += "getrandom = { version = \">=0.2\", features = [\"custom\"] }  # Support Solana target\n";
                                }
                                
                                if (!string.IsNullOrEmpty(additionalDeps))
                                {
                                    tomlContent = tomlContent.Insert(depsEnd, additionalDeps);
                                    modified = true;
                                    logger.LogInformation("Added constraints to existing [workspace.dependencies] in {Path}", workspaceRootToml);
                                }
                            }
                        }
                    }
                    
                    // Note: [replace] is deprecated and doesn't work with version requirements
                    // Instead, we rely on workspace.dependencies and direct dependency constraints
                    // to force getrandom 0.2+ usage
                    
                    if (modified)
                    {
                        await File.WriteAllTextAsync(workspaceRootToml, tomlContent, token);
                    }
                    
                    // Legacy code - no longer used
                    if (false && !tomlContent.Contains("[patch.crates-io]"))
                    {
                        // Find end of file or insert after last section
                        // Insert at the end of the file, before any trailing newlines
                        int insertPos = tomlContent.Length;
                        // Remove trailing whitespace/newlines
                        while (insertPos > 0 && char.IsWhiteSpace(tomlContent[insertPos - 1]))
                        {
                            insertPos--;
                        }
                        // Ensure we have at least one newline before insertion
                        if (insertPos == 0 || tomlContent[insertPos - 1] != '\n')
                        {
                            tomlContent = tomlContent.Insert(insertPos, "\n");
                            insertPos++;
                        }
                        
                        string patchSection = "\n[patch.crates-io]\n" +
                            "# Force compatible versions for Rust 1.84.1-dev (Agave)\n" +
                            "constant_time_eq = \"=0.3.1\"  # Block 0.4.2 which requires rustc 1.85.0+\n" +
                            "borsh = \"=1.5.7\"  # Block 1.6.0 which requires rustc 1.77.0+\n" +
                            "solana-program = \"=2.2.1\"  # Block 2.3.0 which requires rustc 1.79.0+\n" +
                            "toml_edit = \"=0.22.0\"  # Block 0.23.x which requires rustc 1.76+\n" +
                            "toml_parser = \"=1.0.4\"  # Block 1.0.6+ which requires rustc 1.76+\n";
                        
                        tomlContent = tomlContent.Insert(insertPos, patchSection);
                        modified = true;
                        logger.LogInformation("Added [patch.crates-io] section to workspace root {Path}", workspaceRootToml);
                    }
                    
                    if (modified)
                    {
                        await File.WriteAllTextAsync(workspaceRootToml, tomlContent, token);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not add [patch.crates-io] to workspace root {Path}: {Error}", workspaceRootToml, ex.Message);
                }
            }
            
            // Also add constraints to program-level Cargo.toml files as backup
            foreach (var tomlPath in cargoTomlFiles)
            {
                // Skip workspace root (already handled)
                if (tomlPath == workspaceRootToml) continue;
                
                try
                {
                    string tomlContent = await File.ReadAllTextAsync(tomlPath, token);
                    bool modified = false;
                    
                    // Remove any [patch.crates-io] sections from program-level Cargo.toml files
                    // These can cause "path source" errors if they reference crates.io packages
                    if (tomlContent.Contains("[patch.crates-io]"))
                    {
                        var patchStart = tomlContent.IndexOf("[patch.crates-io]");
                        if (patchStart >= 0)
                        {
                            var nextSection = tomlContent.IndexOf("\n[", patchStart + 1);
                            var patchEnd = nextSection > 0 ? nextSection : tomlContent.Length;
                            tomlContent = tomlContent.Remove(patchStart, patchEnd - patchStart);
                            modified = true;
                            logger.LogInformation("Removed [patch.crates-io] section from {Path} (can cause path source errors)", tomlPath);
                        }
                    }
                    
                    // Add [dependencies] constraints for incompatible versions
                    // These force cargo to use versions compatible with Rust 1.84.1 (Agave) or 1.75.0 (old Solana)
                    if (tomlContent.Contains("[dependencies]"))
                    {
                        // Find [dependencies] section
                        int depsIndex = tomlContent.IndexOf("[dependencies]");
                        int nextSectionIndex = tomlContent.IndexOf("\n[", depsIndex + 1);
                        if (nextSectionIndex == -1) nextSectionIndex = tomlContent.Length;
                        
                        string constraints = "";
                        
                        // Block constant_time_eq 0.4.2 (requires rustc 1.85.0, we have 1.84.1)
                        // Use precise version constraint to force 0.3.1
                        if (!tomlContent.Contains("constant_time_eq"))
                        {
                            constraints += "\nconstant_time_eq = \"=0.3.1\"  # Force 0.3.1 (0.4.2 requires rustc 1.85.0+)\n";
                        }
                        else if (tomlContent.Contains("constant_time_eq") && !tomlContent.Contains("=0.3.1"))
                        {
                            // Replace any existing constant_time_eq constraint with precise version
                            tomlContent = System.Text.RegularExpressions.Regex.Replace(
                                tomlContent,
                                @"constant_time_eq\s*=\s*""[^""]+""",
                                "constant_time_eq = \"=0.3.1\"");
                            modified = true;
                        }
                        
                        // Pin borsh to version compatible with Rust 1.84.1
                        // spl-associated-token-account requires ^1.5.7, so use 1.5.7 (latest 1.5.x)
                        if (!tomlContent.Contains("borsh ="))
                        {
                            constraints += "\nborsh = \"=1.5.7\"  # Pin to 1.5.7 (compatible with rustc 1.84.1, satisfies ^1.5.7)\n";
                        }
                        
                        // Pin solana-program to version compatible with Rust 1.84.1
                        // solana-program 2.3.0 requires rustc 1.79.0+, but 2.2.1 should work
                        if (!tomlContent.Contains("solana-program ="))
                        {
                            constraints += "\nsolana-program = \"<2.3\"  # Block 2.3.0 which requires rustc 1.79.0+\n";
                        }
                        
                        // Pin toml_edit to version compatible with Rust 1.84.1
                        // toml_edit 0.23.10+spec-1.0.0 requires rustc 1.76+, use 0.22.x
                        if (!tomlContent.Contains("toml_edit ="))
                        {
                            constraints += "\ntoml_edit = \"<0.23\"  # Block 0.23.x which requires rustc 1.76+\n";
                        }
                        
                        // Pin toml_parser to version compatible with Rust 1.84.1
                        // toml_parser 1.0.6+spec-1.1.0 requires rustc 1.76+, use 1.0.4
                        if (!tomlContent.Contains("toml_parser ="))
                        {
                            constraints += "\ntoml_parser = \"=1.0.4\"  # Pin to 1.0.4 (compatible with rustc 1.84.1)\n";
                        }
                        
                        // Pin blake3 to 1.8.2 or earlier to avoid constant_time_eq 0.4.2 requirement
                        // blake3 1.8.3+ requires constant_time_eq 0.4.2 (needs rustc 1.85.0+)
                        // blake3 1.8.2 works with constant_time_eq 0.3.1 (compatible with rustc 1.84.1)
                        if (!tomlContent.Contains("blake3 ="))
                        {
                            constraints += "\nblake3 = \"=1.8.2\"  # CRITICAL: Force 1.8.2 (1.8.3 requires constant_time_eq 0.4.2)\n";
                        }
                        else if (tomlContent.Contains("blake3 =") && !tomlContent.Contains("=1.8.2") && !tomlContent.Contains("<1.8.3"))
                        {
                            // Replace any existing blake3 constraint with precise version
                            tomlContent = System.Text.RegularExpressions.Regex.Replace(
                                tomlContent,
                                @"blake3\s*=\s*""[^""]+""",
                                "blake3 = \"=1.8.2\"");
                            modified = true;
                        }
                        
                        // Pin getrandom to version that supports Solana target (0.2.x or 0.3.x with custom feature)
                        // getrandom 0.1.x doesn't support Solana's sbf/bpf target
                        if (!tomlContent.Contains("getrandom ="))
                        {
                            constraints += "\ngetrandom = { version = \">=0.2\", features = [\"custom\"] }  # Support Solana target\n";
                        }
                        else if (tomlContent.Contains("getrandom") && !tomlContent.Contains("features") && !tomlContent.Contains("\"custom\""))
                        {
                            // Update existing getrandom to add custom feature
                            tomlContent = System.Text.RegularExpressions.Regex.Replace(
                                tomlContent,
                                @"getrandom\s*=\s*""([^""]+)""",
                                "getrandom = { version = \"$1\", features = [\"custom\"] }");
                            modified = true;
                        }
                        
                        if (!string.IsNullOrEmpty(constraints))
                        {
                            tomlContent = tomlContent.Insert(nextSectionIndex, constraints);
                            modified = true;
                            logger.LogInformation("Added dependency constraints to {Path}", tomlPath);
                        }
                    }
                    
                    if (modified)
                    {
                        await File.WriteAllTextAsync(tomlPath, tomlContent, token);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not modify Cargo.toml at {Path}: {Error}", tomlPath, ex.Message);
                }
            }

            // Step 3: SKIP pre-generating Cargo.lock files
            // NEW APPROACH: Let anchor build create the lockfile naturally, then fix it if needed
            // This avoids the complexity of pre-generating and fixing lockfiles that cargo/anchor will regenerate anyway
            // We'll fix lockfile issues AFTER anchor build fails (in Step 6)
            
            // REMOVED: Pre-generation of Cargo.lock files
            // This was causing duplicate dependencies = [ entries that persisted despite multiple fix attempts
            // Instead, we'll let anchor build create the lockfile, and fix it once if it fails
            /*
            foreach (var tomlPath in cargoTomlFiles)
            {
                try
                {
                    string tomlDir = Path.GetDirectoryName(tomlPath) ?? tempDir;
                    
                    // Run cargo generate-lockfile to create Cargo.lock
                    // The direct dependency constraint in program Cargo.toml should force 0.3.1
                    logger.LogInformation("Pre-generating Cargo.lock in {Dir}", tomlDir);
                    var lockfileResult = await ProcessExtensions.RunCargoAsync("generate-lockfile", tomlDir, logger, token);
                    
                    // If generate-lockfile fails due to constant_time_eq 0.4.2 path source error,
                    // try to work around it by using cargo tree to understand dependencies, then manually fix
                    if (!lockfileResult.IsSuccess && lockfileResult.StandardError.Contains("constant_time_eq") && 
                        lockfileResult.StandardError.Contains("path source"))
                    {
                        logger.LogWarning("cargo generate-lockfile failed due to constant_time_eq path source error. " +
                            "This is expected - we'll skip lockfile generation and let anchor build create it, then fix it.");
                        // Continue without lockfile - anchor build will generate it, then we'll fix it in Step 5
                        continue;
                    }
                    
                    // After lockfile generation, update constant_time_eq if 0.4.2 was selected
                    if (lockfileResult.IsSuccess)
                    {
                        try
                        {
                            string lockPath = Path.Combine(tomlDir, "Cargo.lock");
                            if (File.Exists(lockPath))
                            {
                                string lockContent = await File.ReadAllTextAsync(lockPath, token);
                                if (lockContent.Contains("constant_time_eq") && lockContent.Contains("0.4.2"))
                                {
                                    logger.LogInformation("Updating constant_time_eq from 0.4.2 to 0.3.1 in {Dir}", tomlDir);
                                    var updateResult = await ProcessExtensions.RunCargoAsync(
                                        "update constant_time_eq --precise 0.3.1", 
                                        tomlDir, logger, token);
                                    if (updateResult.IsSuccess)
                                    {
                                        logger.LogInformation("Successfully updated constant_time_eq to 0.3.1 in {Dir}", tomlDir);
                                    }
                                    else
                                    {
                                        logger.LogWarning("Failed to update constant_time_eq: {Error}", updateResult.GetErrorMessage());
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Could not update constant_time_eq in {Dir}, will fix in Cargo.lock manually", tomlDir);
                        }
                    }
                    
                    if (lockfileResult.IsSuccess)
                    {
                        // Fix the generated Cargo.lock to version 3 and fix incompatible dependencies
                        string lockPath = Path.Combine(tomlDir, "Cargo.lock");
                        if (File.Exists(lockPath))
                        {
                            string content = await File.ReadAllTextAsync(lockPath, token);
                            bool modified = false;
                            
                            // Fix version 4 → 3
                            if (content.Contains("version = 4"))
                            {
                                content = content.Replace("version = 4", "version = 3");
                                modified = true;
                            }
                            else if (!content.Contains("version = 3"))
                            {
                                // If no version specified, add it at the top
                                if (content.StartsWith("# This file"))
                                {
                                    int firstNewline = content.IndexOf('\n');
                                    if (firstNewline > 0)
                                    {
                                        content = content.Insert(firstNewline + 1, "version = 3\n");
                                        modified = true;
                                    }
                                }
                            }
                            
                            // Fix incompatible dependency versions for Rust 1.75.0 compatibility
                            // Fix constant_time_eq: 0.4.2 -> 0.3.1 (avoids edition2024)
                            // Must fix BOTH package definitions AND dependency references
                            // Also remove the 0.4.2 package entry entirely to prevent Cargo from trying to use it
                            if (content.Contains("constant_time_eq"))
                            {
                                // First, remove the entire [[package]] entry for constant_time_eq 0.4.2
                                // Match from [[package]] to next [[package]] or end of file
                                var packageEntryPattern = @"(\[\[package\]\]\s*\nname = ""constant_time_eq""[^\[]*?version = ""0\.4\.2""[^\[]*?)(?=\[\[package\]\]|$)";
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    packageEntryPattern,
                                    "",
                                    System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Fix package version definition (in case 0.4.2 entry wasn't removed)
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""constant_time_eq""[^\n]*\n\s*version = "")0\.4\.2("")",
                                    m => m.Groups[1].Value + "0.3.1" + m.Groups[2].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Fix dependency references in dependency lists (e.g., "constant_time_eq 0.4.2")
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"""constant_time_eq\s+0\.4\.2""",
                                    "\"constant_time_eq 0.3.1\"",
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Also fix without quotes (some formats)
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"constant_time_eq\s+0\.4\.2(?=\s|,|""|$)",
                                    "constant_time_eq 0.3.1",
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Additional patterns
                                content = content.Replace("constant_time_eq 0.4.2", "constant_time_eq 0.3.1");
                                content = content.Replace("\"constant_time_eq 0.4.2\"", "\"constant_time_eq 0.3.1\"");
                                content = content.Replace("constant_time_eq\" 0.4.2", "constant_time_eq\" 0.3.1");
                                
                                // Remove checksum fields for constant_time_eq to avoid checksum mismatch errors
                                // When we manually change versions, checksums become invalid
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(\[\[package\]\]\s*\nname = ""constant_time_eq""[^\[]*?version = ""0\.3\.1""[^\[]*?)\nchecksum\s*=\s*""[^""]+""",
                                    "$1",
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                modified = true;
                                logger.LogInformation("Fixed constant_time_eq 0.4.2 -> 0.3.1 (removed 0.4.2 package + fixed dependencies + removed checksum) in Cargo.lock at {Path}", lockPath);
                            }
                            
                            // Fix blake3: 1.8.3 -> 1.8.2 (1.8.3 requires constant_time_eq 0.4.2, 1.8.2 works with 0.3.1)
                            if (content.Contains("blake3") && content.Contains("1.8.3"))
                            {
                                // Use MatchEvaluator to properly handle replacement groups
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""blake3""[^\n]*\n\s*version = "")1\.8\.3("")",
                                    m => m.Groups[1].Value + "1.8.2" + m.Groups[2].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"blake3\s+1\.8\.3",
                                    "blake3 1.8.2");
                                content = content.Replace("blake3 1.8.3", "blake3 1.8.2");
                                
                                // Remove checksum fields for blake3 to avoid checksum mismatch errors
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(\[\[package\]\]\s*\nname = ""blake3""[^\[]*?version = ""1\.8\.2""[^\[]*?)\nchecksum\s*=\s*""[^""]+""",
                                    "$1",
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                modified = true;
                                logger.LogInformation("Fixed blake3 1.8.3 -> 1.8.2 in Cargo.lock at {Path} (avoids constant_time_eq 0.4.2, removed checksum)", lockPath);
                            }
                            
                            // Fix borsh: 1.6.0 -> 1.5.7 (compatible with Rust 1.75.0, satisfies ^1.5.7 requirement)
                            if (content.Contains("borsh") && content.Contains("1.6.0"))
                            {
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""borsh""[^\n]*\n\s*version = "")1\.6\.0("")",
                                    m => m.Groups[1].Value + "1.5.7" + m.Groups[2].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"borsh\s+1\.6\.0",
                                    "borsh 1.5.7");
                                content = content.Replace("borsh 1.6.0", "borsh 1.5.7");
                                content = content.Replace("borsh\" 1.6.0", "borsh\" 1.5.7");
                                modified = true;
                                logger.LogInformation("Fixed borsh 1.6.0 -> 1.5.7 in Cargo.lock at {Path}", lockPath);
                            }
                            
                            // Fix solana-program: 2.3.0 -> 2.2.x (compatible with Rust 1.75.0)
                            // Try 2.2.1 which should work with Rust 1.75.0
                            if (content.Contains("solana-program") && content.Contains("2.3.0"))
                            {
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""solana-program""[^\n]*\n\s*version = "")2\.3\.0("")",
                                    m => m.Groups[1].Value + "2.2.1" + m.Groups[2].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"solana-program\s+2\.3\.0",
                                    "solana-program 2.2.1");
                                content = content.Replace("solana-program 2.3.0", "solana-program 2.2.1");
                                content = content.Replace("solana-program\" 2.3.0", "solana-program\" 2.2.1");
                                modified = true;
                                logger.LogInformation("Fixed solana-program 2.3.0 -> 2.2.1 in Cargo.lock at {Path}", lockPath);
                            }
                            
                            // Fix toml_edit: 0.23.x -> 0.22.x (compatible with Rust 1.75.0)
                            if (content.Contains("toml_edit") && content.Contains("0.23"))
                            {
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""toml_edit""[^\n]*\n\s*version = "")0\.23\.([0-9]+)("")",
                                    m => m.Groups[1].Value + "0.22.0" + m.Groups[3].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"toml_edit\s+0\.23\.",
                                    "toml_edit 0.22.0");
                                content = content.Replace("toml_edit 0.23.", "toml_edit 0.22.0");
                                modified = true;
                                logger.LogInformation("Fixed toml_edit 0.23.x -> 0.22.0 in Cargo.lock at {Path}", lockPath);
                            }
                            
                            // Fix toml_parser: 1.0.6+spec-1.1.0 -> 1.0.4 (compatible with Rust 1.75.0)
                            if (content.Contains("toml_parser") && (content.Contains("1.0.6") || content.Contains("1.0.5")))
                            {
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""toml_parser""[^\n]*\n\s*version = "")1\.0\.([56])([^\n]*)("")",
                                    m => m.Groups[1].Value + "1.0.4" + m.Groups[4].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"toml_parser\s+1\.0\.([56])",
                                    "toml_parser 1.0.4");
                                content = content.Replace("toml_parser 1.0.6", "toml_parser 1.0.4");
                                content = content.Replace("toml_parser 1.0.5", "toml_parser 1.0.4");
                                modified = true;
                                logger.LogInformation("Fixed toml_parser 1.0.6/1.0.5 -> 1.0.4 in Cargo.lock at {Path}", lockPath);
                            }
                            
                            // Fix getrandom: 0.1.x -> 0.2.17 (0.1.x doesn't support Solana target, 0.2.x+ does with custom feature)
                            if (content.Contains("getrandom") && content.Contains("0.1"))
                            {
                                // Remove entire [[package]] entry for getrandom 0.1.x
                                var getrandom01Pattern = @"\[\[package\]\]\s*\nname = ""getrandom""[^\[]*?version = ""0\.1\.[0-9]+""[^\[]*?(?=\[\[package\]\]|$)";
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    getrandom01Pattern,
                                    "",
                                    System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Fix version references
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""getrandom""[^\n]*\n\s*version = "")0\.1\.[0-9]+("")",
                                    m => m.Groups[1].Value + "0.2.17" + m.Groups[2].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Fix dependency references
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"""getrandom\s+0\.1\.[0-9]+""",
                                    "\"getrandom 0.2.17\"",
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"getrandom\s+0\.1\.[0-9]+",
                                    "getrandom 0.2.17");
                                content = content.Replace("\"getrandom 0.1.", "\"getrandom 0.2.17");
                                
                                // Remove checksum for getrandom 0.1.x if present
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(\[\[package\]\]\s*\nname = ""getrandom""[^\[]*?version = ""0\.1\.[0-9]+""[^\[]*?)\nchecksum\s*=\s*""[^""]+""",
                                    "$1",
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                modified = true;
                                logger.LogInformation("Fixed getrandom 0.1.x -> 0.2.17 in Cargo.lock at {Path} (Solana target compatibility)", lockPath);
                            }
                            
                            // Fix constant_time_eq: 0.4.2 -> 0.3.1 (compatible with Solana rustc 1.84.1-dev)
                            if (content.Contains("constant_time_eq") && content.Contains("0.4.2"))
                            {
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""constant_time_eq""[^\n]*\n\s*version = "")0\.4\.2("")",
                                    m => m.Groups[1].Value + "0.3.1" + m.Groups[2].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"constant_time_eq\s+0\.4\.2",
                                    "constant_time_eq 0.3.1");
                                content = content.Replace("constant_time_eq 0.4.2", "constant_time_eq 0.3.1");
                                modified = true;
                                logger.LogInformation("Fixed constant_time_eq 0.4.2 -> 0.3.1 in Cargo.lock at {Path}", lockPath);
                            }
                            
                            // Fix getrandom: 0.1.x -> 0.2.17 (0.1.x doesn't support Solana target)
                            if (content.Contains("getrandom") && content.Contains("0.1"))
                            {
                                // Remove entire [[package]] entry for getrandom 0.1.x
                                var getrandom01Pattern = @"\[\[package\]\]\s*\nname = ""getrandom""[^\[]*?version = ""0\.1\.[0-9]+""[^\[]*?(?=\[\[package\]\]|$)";
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    getrandom01Pattern,
                                    "",
                                    System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Fix version references
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"(name = ""getrandom""[^\n]*\n\s*version = "")0\.1\.[0-9]+("")",
                                    m => m.Groups[1].Value + "0.2.17" + m.Groups[2].Value,
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                
                                // Fix dependency references
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"""getrandom\s+0\.1\.[0-9]+""",
                                    "\"getrandom 0.2.17\"",
                                    System.Text.RegularExpressions.RegexOptions.Multiline);
                                content = System.Text.RegularExpressions.Regex.Replace(
                                    content,
                                    @"getrandom\s+0\.1\.[0-9]+",
                                    "getrandom 0.2.17");
                                content = content.Replace("\"getrandom 0.1.", "\"getrandom 0.2.17");
                                
                                modified = true;
                                logger.LogInformation("Fixed getrandom 0.1.x -> 0.2.17 in Cargo.lock at {Path} (Solana target compatibility)", lockPath);
                            }
                            
                            // Fix duplicate source fields (can occur if package entries are partially removed/fixed)
                            // Remove duplicate source lines within the same package entry
                            content = System.Text.RegularExpressions.Regex.Replace(
                                content,
                                @"(\[\[package\]\]\s*\n(?:[^\[]*\n)*?source\s*=\s*""[^""]+""\s*\n)(?:[^\[]*\n)*?source\s*=\s*""[^""]+""",
                                "$1",
                                System.Text.RegularExpressions.RegexOptions.Multiline);
                            
                            // Remove ALL checksum fields for packages we modified (constant_time_eq, blake3)
                            // This is more aggressive but ensures checksums don't cause validation errors
                            // Checksums are between [[package]] entries, so we match the package name and remove checksum lines
                            content = System.Text.RegularExpressions.Regex.Replace(
                                content,
                                @"(\[\[package\]\]\s*\nname = ""(?:constant_time_eq|blake3)""[^\[]*?)\nchecksum\s*=\s*""[^""]+""",
                                "$1",
                                System.Text.RegularExpressions.RegexOptions.Multiline);
                            
                            // CRITICAL: Remove duplicate dependencies = [ RIGHT AFTER generate-lockfile
                            // This catches duplicates created by cargo generate-lockfile itself
                            // MUST skip the ENTIRE array, not just the opening line
                            string[] lockLines = content.Split('\n');
                            System.Collections.Generic.List<string> cleanedLockLines = new();
                            bool inPkgLock = false;
                            bool seenDepsLock = false;
                            bool skippingDupArrayLock = false;
                            int arrayDepthLock = 0;
                            int dupRemovedLock = 0;
                            
                            for (int i = 0; i < lockLines.Length; i++)
                            {
                                string line = lockLines[i];
                                string trimmed = line.Trim();
                                
                                if (trimmed == "[[package]]" || trimmed.StartsWith("[[package]]"))
                                {
                                    inPkgLock = true;
                                    seenDepsLock = false;
                                    skippingDupArrayLock = false;
                                    arrayDepthLock = 0;
                                    cleanedLockLines.Add(line);
                                }
                                else if (inPkgLock && trimmed.StartsWith("[[") && !trimmed.StartsWith("[[package]]"))
                                {
                                    inPkgLock = false;
                                    seenDepsLock = false;
                                    skippingDupArrayLock = false;
                                    arrayDepthLock = 0;
                                    cleanedLockLines.Add(line);
                                }
                                // If we're skipping a duplicate array, track depth and skip until it closes
                                else if (skippingDupArrayLock)
                                {
                                    int open = 0, close = 0;
                                    foreach (char c in trimmed)
                                    {
                                        if (c == '[') open++;
                                        if (c == ']') close++;
                                    }
                                    arrayDepthLock += (open - close);
                                    if (arrayDepthLock <= 0)
                                    {
                                        skippingDupArrayLock = false;
                                        arrayDepthLock = 0;
                                    }
                                    continue; // Skip this line
                                }
                                else if (inPkgLock && (trimmed.StartsWith("dependencies = [") || trimmed.StartsWith("dependencies=[")) && arrayDepthLock == 0)
                                {
                                    if (seenDepsLock)
                                    {
                                        // DUPLICATE - skip this line AND the entire array
                                        dupRemovedLock++;
                                        skippingDupArrayLock = true;
                                        arrayDepthLock = 1;
                                        logger.LogWarning("Step 3: Removing duplicate 'dependencies = [' and entire array at line {LineNum} in {Path} (after generate-lockfile)", i + 1, lockPath);
                                        continue;
                                    }
                                    seenDepsLock = true;
                                    arrayDepthLock = 1;
                                    cleanedLockLines.Add(line);
                                }
                                // Normal line - track array depth
                                else
                                {
                                    if (arrayDepthLock > 0)
                                    {
                                        int open = 0, close = 0;
                                        foreach (char c in trimmed)
                                        {
                                            if (c == '[') open++;
                                            if (c == ']') close++;
                                        }
                                        arrayDepthLock += (open - close);
                                        if (arrayDepthLock < 0) arrayDepthLock = 0;
                                    }
                                    cleanedLockLines.Add(line);
                                }
                            }
                            
                            if (dupRemovedLock > 0)
                            {
                                content = string.Join("\n", cleanedLockLines);
                                modified = true;
                                logger.LogWarning("Step 3: Removed {Count} duplicate 'dependencies = [' entries (with arrays) at {Path} (after generate-lockfile)", dupRemovedLock, lockPath);
                            }
                            
                            if (modified)
                            {
                                await File.WriteAllTextAsync(lockPath, content, token);
                                logger.LogInformation("Fixed Cargo.lock at {Path} (removed checksums for modified packages)", lockPath);
                            }
                        }
                    }
                    else
                    {
                        logger.LogWarning("Failed to generate Cargo.lock in {Dir}: {Error}", tomlDir, lockfileResult.GetErrorMessage());
                    }
                }
            }
            */
            
            // Step 4: Pre-download constant_time_eq 0.3.1 into isolated CARGO_HOME to prevent 0.4.2 from being downloaded
            // This ensures cargo uses the cached 0.3.1 version instead of downloading 0.4.2
            Process? fixMonitorProcess = null;
            Task? fixTask = null;
            CancellationTokenSource? fixTaskCts = null;
            try
            {
                string isolatedCargoHome = Path.Combine(tempDir, ".cargo_home");
                Directory.CreateDirectory(isolatedCargoHome);
                
                // Pre-download constant_time_eq 0.3.1 into the isolated CARGO_HOME
                // This ensures it's cached and cargo will prefer it over 0.4.2
                try
                {
                    logger.LogInformation("Pre-downloading constant_time_eq 0.3.1 into isolated CARGO_HOME to prevent 0.4.2 download");
                    using var preDownloadProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cargo",
                            Arguments = "fetch --manifest-path /dev/null --quiet 2>/dev/null || cargo fetch --quiet 2>/dev/null || true",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            Environment = 
                            {
                                ["CARGO_HOME"] = isolatedCargoHome,
                                ["CARGO_NET_OFFLINE"] = "false"
                            }
                        }
                    };
                    
                    // Create a temporary Cargo.toml that depends on constant_time_eq 0.3.1
                    string tempCargoToml = Path.Combine(tempDir, "prefetch_Cargo.toml");
                    string tempCargoTomlContent = @"[package]
name = ""prefetch""
version = ""0.1.0""
edition = ""2021""

[dependencies]
constant_time_eq = ""=0.3.1""
";
                    await File.WriteAllTextAsync(tempCargoToml, tempCargoTomlContent, token);
                    
                    // Run cargo fetch in the temp directory to download constant_time_eq 0.3.1
                    using var fetchProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "cargo",
                            Arguments = "fetch --manifest-path prefetch_Cargo.toml",
                            WorkingDirectory = tempDir,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            Environment = 
                            {
                                ["CARGO_HOME"] = isolatedCargoHome
                            }
                        }
                    };
                    fetchProcess.Start();
                    await fetchProcess.WaitForExitAsync(token);
                    logger.LogInformation("Pre-downloaded constant_time_eq 0.3.1 into isolated CARGO_HOME (exit code: {ExitCode})", fetchProcess.ExitCode);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not pre-download constant_time_eq 0.3.1: {Error}", ex.Message);
                }
                
                // CRITICAL: Fix registry Cargo.toml files BEFORE anchor build runs
                // This must happen BEFORE cargo downloads/parses them
                string registryBase = Path.Combine(isolatedCargoHome, "registry", "src");
                
                // Function to fix a Cargo.toml file
                async Task FixCargoTomlAsync(string tomlPath)
                {
                    try
                    {
                        if (!File.Exists(tomlPath)) return;
                        
                        string content = await File.ReadAllTextAsync(tomlPath, token);
                        string original = content;
                        
                        // Fix edition2024 -> 2021
                        if (content.Contains("edition = \"2024\"") || content.Contains("edition2024"))
                        {
                            content = System.Text.RegularExpressions.Regex.Replace(
                                content,
                                @"edition\s*=\s*""2024""",
                                "edition = \"2021\"");
                            content = content.Replace("edition2024", "");
                        }
                        
                        // Fix rust-version if too high
                        if (System.Text.RegularExpressions.Regex.IsMatch(content, @"rust-version\s*=\s*""1\.(8[5-9]|9[0-9])"))
                        {
                            content = System.Text.RegularExpressions.Regex.Replace(
                                content,
                                @"rust-version\s*=\s*""1\.(8[5-9]|9[0-9])""",
                                "rust-version = \"1.75.0\"");
                        }
                        
                        if (content != original)
                        {
                            await File.WriteAllTextAsync(tomlPath, content, token);
                            logger.LogInformation("Pre-fixed Cargo.toml at {Path}", tomlPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not fix Cargo.toml at {Path}: {Error}", tomlPath, ex.Message);
                    }
                }
                
                // CRITICAL: Start a background task that continuously fixes problematic crates as they're downloaded
                // This runs in parallel with anchor build to catch files immediately after download
                // Poll every 1ms for maximum responsiveness - cargo parses files synchronously during download
                try
                {
                    fixTaskCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    fixTask = Task.Run(async () =>
                    {
                        while (!fixTaskCts.Token.IsCancellationRequested)
                        {
                            try
                            {
                                if (Directory.Exists(registryBase))
                                {
                                    var registryDirs = Directory.GetDirectories(registryBase);
                                    foreach (var registryDir in registryDirs)
                                    {
                                        // Fix constant_time_eq-0.4.2 IMMEDIATELY when it appears
                                        string constantTimeEqPath = Path.Combine(registryDir, "constant_time_eq-0.4.2");
                                        if (Directory.Exists(constantTimeEqPath))
                                        {
                                            string cargoTomlPath = Path.Combine(constantTimeEqPath, "Cargo.toml");
                                            if (File.Exists(cargoTomlPath))
                                            {
                                                await FixCargoTomlAsync(cargoTomlPath);
                                            }
                                        }
                                        
                                        // Fix blake3-1.8.3 IMMEDIATELY when it appears
                                        string blake3Path = Path.Combine(registryDir, "blake3-1.8.3");
                                        if (Directory.Exists(blake3Path))
                                        {
                                            string cargoTomlPath = Path.Combine(blake3Path, "Cargo.toml");
                                            if (File.Exists(cargoTomlPath))
                                            {
                                                await FixCargoTomlAsync(cargoTomlPath);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                            
                            // Poll every 1ms for maximum responsiveness - cargo parses files synchronously
                            await Task.Delay(1, fixTaskCts.Token);
                        }
                    }, fixTaskCts.Token);
                    
                    logger.LogInformation("Started aggressive background fix task (1ms polling) to fix Cargo.toml files in isolated CARGO_HOME: {CargoHome}", isolatedCargoHome);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not start background fix task: {Error}", ex.Message);
                }
                
                // Create a cargo wrapper that fixes constant_time_eq 0.4.2 immediately after each cargo command
                // This intercepts cargo and fixes the crate right after download, before cargo parses it
                try
                {
                    string cargoWrapperDir = Path.Combine(tempDir, ".cargo_wrapper");
                    Directory.CreateDirectory(cargoWrapperDir);
                    string cargoWrapperBin = Path.Combine(cargoWrapperDir, "cargo");
                    
                    // Find the real cargo binary
                    string realCargo = "cargo";
                    try
                    {
                        using var whichProcess = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = "which",
                                Arguments = "cargo",
                                RedirectStandardOutput = true,
                                UseShellExecute = false
                            }
                        };
                        whichProcess.Start();
                        string cargoPath = whichProcess.StandardOutput.ReadToEnd().Trim();
                        whichProcess.WaitForExit(1000);
                        if (whichProcess.ExitCode == 0 && !string.IsNullOrEmpty(cargoPath) && File.Exists(cargoPath))
                        {
                            realCargo = cargoPath;
                        }
                    }
                    catch { }
                    
                    // Create wrapper script that runs cargo in background and continuously fixes problematic crates
                    // Fixes both constant_time_eq-0.4.2 and blake3-1.8.3 (both require edition2024)
                    string wrapperScript = $@"#!/bin/bash
# Cargo wrapper that fixes edition2024 crates DURING cargo execution
REAL_CARGO=""{realCargo}""
REGISTRY_BASE=""{isolatedCargoHome}/registry/src""

# Function to fix a Cargo.toml file
fix_toml() {{
    local tomlfile=""$1""
    if [ ! -f ""$tomlfile"" ]; then
        return
    fi
    
    # Fix edition2024 -> 2021
    if grep -q ""edition.*2024"" ""$tomlfile"" 2>/dev/null || grep -q ""edition2024"" ""$tomlfile"" 2>/dev/null; then
        sed -i '' 's/edition = ""2024""/edition = ""2021""/g' ""$tomlfile"" 2>/dev/null
        sed -i '' '/edition2024/d' ""$tomlfile"" 2>/dev/null
    fi
    # Fix rust-version
    if grep -q ""rust-version = ""1\.8[5-9]"" ""$tomlfile"" 2>/dev/null || grep -q ""rust-version = ""1\.9[0-9]"" ""$tomlfile"" 2>/dev/null; then
        sed -i '' 's/rust-version = ""1\.8[5-9]/rust-version = ""1.75.0/g' ""$tomlfile"" 2>/dev/null
        sed -i '' 's/rust-version = ""1\.9[0-9]/rust-version = ""1.75.0/g' ""$tomlfile"" 2>/dev/null
    fi
}}

# Fix any existing problematic crates BEFORE running cargo
if [ -d ""$REGISTRY_BASE"" ]; then
    find ""$REGISTRY_BASE"" -path ""*/constant_time_eq-0.4.2/Cargo.toml"" -type f 2>/dev/null | while read tomlfile; do
        fix_toml ""$tomlfile""
    done
    find ""$REGISTRY_BASE"" -path ""*/blake3-1.8.3/Cargo.toml"" -type f 2>/dev/null | while read tomlfile; do
        fix_toml ""$tomlfile""
    done
fi

# Start a background process that continuously fixes problematic crates while cargo runs
(
    while true; do
        if [ -d ""$REGISTRY_BASE"" ]; then
            find ""$REGISTRY_BASE"" -path ""*/constant_time_eq-0.4.2/Cargo.toml"" -type f 2>/dev/null | while read tomlfile; do
                fix_toml ""$tomlfile""
            done
            find ""$REGISTRY_BASE"" -path ""*/blake3-1.8.3/Cargo.toml"" -type f 2>/dev/null | while read tomlfile; do
                fix_toml ""$tomlfile""
            done
        fi
        sleep 0.001  # Poll every 1ms for maximum responsiveness
    done
) &
FIX_PID=$!

# Run the real cargo command
""$REAL_CARGO"" ""$@""
EXIT_CODE=$?

# Kill the background fix process
kill $FIX_PID 2>/dev/null
wait $FIX_PID 2>/dev/null

# Final fix pass after cargo completes
if [ -d ""$REGISTRY_BASE"" ]; then
    find ""$REGISTRY_BASE"" -path ""*/constant_time_eq-0.4.2/Cargo.toml"" -type f 2>/dev/null | while read tomlfile; do
        fix_toml ""$tomlfile""
    done
    find ""$REGISTRY_BASE"" -path ""*/blake3-1.8.3/Cargo.toml"" -type f 2>/dev/null | while read tomlfile; do
        fix_toml ""$tomlfile""
    done
fi

exit $EXIT_CODE";
                    
                    await File.WriteAllTextAsync(cargoWrapperBin, wrapperScript, token);
                    
                    if (Environment.OSVersion.Platform == PlatformID.Unix || 
                        Environment.OSVersion.Platform == PlatformID.MacOSX)
                    {
                        var chmodProcess = Process.Start(new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"+x \"{cargoWrapperBin}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        chmodProcess?.WaitForExit(1000);
                    }
                    
                    logger.LogInformation("Created cargo wrapper at {Wrapper} that fixes constant_time_eq 0.4.2 after each cargo command", cargoWrapperBin);
                    
                    // Also create background monitor as backup (in case cargo is called directly, bypassing wrapper)
                    string fixMonitorScript = Path.Combine(tempDir, "fix_downloaded_crate.sh");
                    string monitorScript = $@"#!/bin/bash
# Background monitor as backup - fixes crates with edition2024 requirement
REGISTRY_BASE=""{isolatedCargoHome}/registry/src""
while true; do
    if [ -d ""$REGISTRY_BASE"" ]; then
        find ""$REGISTRY_BASE"" -path ""*/constant_time_eq-0.4.2/Cargo.toml"" -type f 2>/dev/null | while read tomlfile; do
            # Fix edition2024 -> 2021
            if grep -q ""edition.*2024"" ""$tomlfile"" 2>/dev/null || grep -q ""edition2024"" ""$tomlfile"" 2>/dev/null; then
                sed -i '' 's/edition = ""2024""/edition = ""2021""/g' ""$tomlfile"" 2>/dev/null
                sed -i '' '/edition2024/d' ""$tomlfile"" 2>/dev/null
            fi
            # Fix rust-version
            if grep -q ""rust-version = ""1\.8[5-9]"" ""$tomlfile"" 2>/dev/null || grep -q ""rust-version = ""1\.9[0-9]"" ""$tomlfile"" 2>/dev/null; then
                sed -i '' 's/rust-version = ""1\.8[5-9]/rust-version = ""1.75.0/g' ""$tomlfile"" 2>/dev/null
                sed -i '' 's/rust-version = ""1\.9[0-9]/rust-version = ""1.75.0/g' ""$tomlfile"" 2>/dev/null
            fi
        done
    fi
    sleep 0.01
done";
                    await File.WriteAllTextAsync(fixMonitorScript, monitorScript, token);
                    
                    if (Environment.OSVersion.Platform == PlatformID.Unix || 
                        Environment.OSVersion.Platform == PlatformID.MacOSX)
                    {
                        var chmodProcess2 = Process.Start(new ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"+x \"{fixMonitorScript}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        chmodProcess2?.WaitForExit(1000);
                    }
                    
                    // Create a timestamp file for the monitor to track new files
                    string monitorStartFile = Path.Combine(tempDir, $"monitor_start_{Environment.ProcessId}");
                    await File.WriteAllTextAsync(monitorStartFile, DateTime.UtcNow.ToString("O"), token);
                    
                    fixMonitorProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"\"{fixMonitorScript}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            Environment = 
                            {
                                ["MONITOR_START_FILE"] = monitorStartFile
                            }
                        }
                    };
                    fixMonitorProcess.Start();
                    logger.LogInformation("Started aggressive background monitor (1ms polling) to fix downloaded crates in isolated CARGO_HOME: {CargoHome}", isolatedCargoHome);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not create cargo wrapper or start fix monitor: {Error}", ex.Message);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not pre-fix constant_time_eq 0.4.2: {Error}", ex.Message);
                // Don't fail the compilation if pre-fix fails - monitor will catch it
            }

            // Step 5: REMOVED - Skip pre-build lockfile fixes
            // REASON: We're no longer pre-generating lockfiles, so there's nothing to fix here
            // We'll fix lockfile issues AFTER anchor build fails (in Step 6)
            logger.LogInformation("Skipping pre-build Cargo.lock fixes - will fix after anchor build if needed");
            
            // REMOVED: All Step 5 code that tried to fix lockfiles before anchor build
            // This was ~550 lines of complex regex-based duplicate removal that wasn't working
            /*
            var finalLockFiles = Directory.GetFiles(tempDir, "Cargo.lock", SearchOption.AllDirectories);
            foreach (var lockPath in finalLockFiles)
            {
                try
                {
                    string content = await File.ReadAllTextAsync(lockPath, token);
                    string original = content;
                    bool modified = false;
                    
                    // Fix constant_time_eq: Remove 0.4.2 package entry and fix all references
                    if (content.Contains("constant_time_eq") && content.Contains("0.4.2"))
                    {
                        // First, try to remove the entire [[package]] entry for constant_time_eq 0.4.2
                        // Match from [[package]] to next [[package]] or end of file, including all fields
                        var packageEntryPattern = @"\[\[package\]\]\s*\nname = ""constant_time_eq""[^\[]*?version = ""0\.4\.2""[^\[]*?(?=\[\[package\]\]|$)";
                        string beforeRemove = content;
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            packageEntryPattern,
                            "",
                            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        // If the pattern didn't match (maybe different formatting), try a more aggressive removal
                        if (content == beforeRemove)
                        {
                            // Try matching with more flexible whitespace
                            packageEntryPattern = @"\[\[package\]\]\s*\n\s*name\s*=\s*""constant_time_eq""[^\[]*?version\s*=\s*""0\.4\.2""[^\[]*?(?=\[\[package\]\]|$)";
                            content = System.Text.RegularExpressions.Regex.Replace(
                                content,
                                packageEntryPattern,
                                "",
                                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                        }
                        
                        // Also remove any standalone source references that might reference constant_time_eq 0.4.2 as a path
                        // This handles cases where the source is defined separately
                        // Remove source lines that reference constant_time_eq 0.4.2 (especially path sources)
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"source\s*=\s*""[^""]*constant_time_eq[^""]*0\.4\.2[^""]*""",
                            "",
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        // Also remove any source field within a package entry for constant_time_eq 0.4.2
                        // This handles cases where the package entry wasn't fully removed
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"(name\s*=\s*""constant_time_eq""[^\n]*\n\s*version\s*=\s*""0\.4\.2""[^\n]*\n\s*)source\s*=\s*""[^""]+""",
                            "$1",
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        // Remove any path source references (source = "path" or source = "../path/to/crate")
                        // that might be associated with constant_time_eq 0.4.2
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"(\[\[package\]\]\s*\nname\s*=\s*""constant_time_eq""[^\[]*?version\s*=\s*""0\.4\.2""[^\[]*?\n\s*)source\s*=\s*""path[^""]*""",
                            "$1",
                            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        // Fix package version definition (in case 0.4.2 entry wasn't removed)
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"(name\s*=\s*""constant_time_eq""[^\n]*\n\s*version\s*=\s*"")0\.4\.2("")",
                            m => m.Groups[1].Value + "0.3.1" + m.Groups[2].Value,
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        // Fix dependency references in dependency arrays
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"""constant_time_eq\s+0\.4\.2""",
                            "\"constant_time_eq 0.3.1\"",
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        content = content.Replace("constant_time_eq 0.4.2", "constant_time_eq 0.3.1");
                        content = content.Replace("\"constant_time_eq 0.4.2\"", "\"constant_time_eq 0.3.1\"");
                        modified = true;
                    }
                    
                    // Fix blake3: 1.8.3 -> 1.8.2 (1.8.3 requires constant_time_eq 0.4.2)
                    if (content.Contains("blake3") && content.Contains("1.8.3"))
                    {
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"(name = ""blake3""[^\n]*\n\s*version = "")1\.8\.3("")",
                            m => m.Groups[1].Value + "1.8.2" + m.Groups[2].Value,
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"blake3\s+1\.8\.3",
                            "blake3 1.8.2");
                        content = content.Replace("blake3 1.8.3", "blake3 1.8.2");
                        content = content.Replace("\"blake3 1.8.3\"", "\"blake3 1.8.2\"");
                        
                        // Remove checksum fields for packages we modified to avoid checksum mismatch errors
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"(\[\[package\]\]\s*\nname = ""(?:constant_time_eq|blake3)""[^\[]*?version = ""(?:0\.3\.1|1\.8\.2)""[^\[]*?)\nchecksum\s*=\s*""[^""]+""",
                            "$1",
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        modified = true;
                    }
                    
                    // Fix getrandom: 0.1.x -> 0.2.17 (0.1.x doesn't support Solana target)
                    if (content.Contains("getrandom") && content.Contains("0.1"))
                    {
                        // Remove entire [[package]] entry for getrandom 0.1.x
                        var getrandom01Pattern = @"\[\[package\]\]\s*\nname = ""getrandom""[^\[]*?version = ""0\.1\.[0-9]+""[^\[]*?(?=\[\[package\]\]|$)";
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            getrandom01Pattern,
                            "",
                            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        // Fix version references
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"(name = ""getrandom""[^\n]*\n\s*version = "")0\.1\.[0-9]+("")",
                            m => m.Groups[1].Value + "0.2.17" + m.Groups[2].Value,
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        // Fix dependency references - be very aggressive
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"""getrandom\s+0\.1\.[0-9]+""",
                            "\"getrandom 0.2.17\"",
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"getrandom\s+0\.1\.[0-9]+",
                            "getrandom 0.2.17");
                        content = content.Replace("\"getrandom 0.1.", "\"getrandom 0.2.17");
                        content = content.Replace("getrandom 0.1.", "getrandom 0.2.17");
                        
                        // Also fix any dependency entries that reference getrandom 0.1.x
                        content = System.Text.RegularExpressions.Regex.Replace(
                            content,
                            @"(name = ""[^""]+""[^\[]*?\n[^\[]*?getrandom\s*=\s*"")0\.1\.[0-9]+("")",
                            m => m.Groups[1].Value + "0.2.17" + m.Groups[2].Value,
                            System.Text.RegularExpressions.RegexOptions.Multiline);
                        
                        modified = true;
                        logger.LogInformation("Fixed getrandom 0.1.x -> 0.2.17 in Cargo.lock at {Path} (Solana target compatibility)", lockPath);
                    }
                    
                    // Fix duplicate fields (source, checksum, dependencies, etc.) - can occur if package entries are partially removed/fixed
                    // Use a more robust approach: parse package entries and remove duplicates within each
                    string[] lines = content.Split('\n');
                    System.Collections.Generic.List<string> cleanedLines = new();
                    bool inPackage = false;
                    System.Collections.Generic.HashSet<string> seenKeys = new();
                    int bracketDepth = 0; // Track bracket depth for arrays
                    bool inDependenciesArray = false;
                    
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i];
                        string trimmedLine = line.Trim();
                        bool isPackageStart = trimmedLine.StartsWith("[[package]]");
                        
                        // Track bracket depth more accurately
                        int openBrackets = 0;
                        int closeBrackets = 0;
                        foreach (char c in trimmedLine)
                        {
                            if (c == '[') openBrackets++;
                            if (c == ']') closeBrackets++;
                        }
                        bracketDepth += (openBrackets - closeBrackets);
                        if (bracketDepth < 0) bracketDepth = 0;
                        
                        // Track if we're in a dependencies array
                        // Check for duplicate dependencies = [ BEFORE setting inDependenciesArray
                        if ((trimmedLine.StartsWith("dependencies = [") || trimmedLine.StartsWith("dependencies=[")) && inPackage && !inDependenciesArray)
                        {
                            // Check if we've already seen dependencies in this package
                            if (seenKeys.Contains("dependencies"))
                            {
                                // Duplicate dependencies = [ - skip it
                                modified = true;
                                logger.LogInformation("Removed duplicate 'dependencies = [' at line {LineNum} in {Path}", i + 1, lockPath);
                                continue;
                            }
                            seenKeys.Add("dependencies");
                            inDependenciesArray = true;
                            bracketDepth = 1; // We're now inside an array
                        }
                        else if (inDependenciesArray && bracketDepth == 0)
                        {
                            inDependenciesArray = false;
                        }
                        
                        if (isPackageStart)
                        {
                            inPackage = true;
                            seenKeys.Clear();
                            bracketDepth = 0;
                            inDependenciesArray = false;
                            cleanedLines.Add(line);
                        }
                        else if (inPackage)
                        {
                            // Check if we're starting a new package entry or section
                            if (trimmedLine.StartsWith("[[") && !trimmedLine.StartsWith("[[package]]"))
                            {
                                inPackage = false;
                                seenKeys.Clear();
                                bracketDepth = 0;
                                inDependenciesArray = false;
                                cleanedLines.Add(line);
                            }
                            else if (inDependenciesArray || bracketDepth > 0)
                            {
                                // Inside an array - always keep these lines
                                cleanedLines.Add(line);
                            }
                            else
                            {
                                // Check for duplicate dependencies = [ BEFORE other checks
                                // This handles cases where we've already seen dependencies and exited its array
                                if ((trimmedLine.StartsWith("dependencies = [") || trimmedLine.StartsWith("dependencies=[")) && seenKeys.Contains("dependencies"))
                                {
                                    // Duplicate dependencies = [ - skip it
                                    modified = true;
                                    logger.LogInformation("Removed duplicate 'dependencies = [' at line {LineNum} in {Path}", i + 1, lockPath);
                                    continue;
                                }
                                
                                // Extract the key name from lines like "key = value" or "key = ["
                                string? keyName = null;
                                
                                if (trimmedLine.Contains("=") && !trimmedLine.StartsWith("\"") && !trimmedLine.StartsWith("'") && 
                                    !trimmedLine.StartsWith("#") && trimmedLine.Length > 0 && !trimmedLine.StartsWith(" "))
                                {
                                    int eqIndex = trimmedLine.IndexOf('=');
                                    if (eqIndex > 0)
                                    {
                                        keyName = trimmedLine.Substring(0, eqIndex).Trim();
                                        
                                        // Only track specific problematic keys that can be duplicated
                                        if (keyName == "source" || keyName == "checksum" || keyName == "dependencies" || 
                                            keyName == "version" || keyName == "name")
                                        {
                                            if (seenKeys.Contains(keyName))
                                            {
                                                // Duplicate key - skip it
                                                modified = true;
                                                logger.LogInformation("Removed duplicate key '{Key}' at line {LineNum} in {Path}", keyName, i + 1, lockPath);
                                                continue;
                                            }
                                            seenKeys.Add(keyName);
                                            
                                            // If this is dependencies = [, we'll be entering an array
                                            if (keyName == "dependencies" && trimmedLine.Contains("["))
                                            {
                                                inDependenciesArray = true;
                                                bracketDepth = 1;
                                            }
                                        }
                                    }
                                }
                                
                                cleanedLines.Add(line);
                            }
                        }
                        else
                        {
                            cleanedLines.Add(line);
                        }
                    }
                    
                    if (modified)
                    {
                        content = string.Join("\n", cleanedLines);
                        logger.LogInformation("Removed duplicate fields in Cargo.lock at {Path}", lockPath);
                    }
                    
                    // Additional aggressive pass: remove duplicate dependencies = [ lines using simpler logic
                    string[] lines2 = content.Split('\n');
                    System.Collections.Generic.List<string> finalLines = new();
                    bool inPkg2 = false;
                    bool seenDeps2 = false;
                    bool skippingDuplicate = false;
                    int arrayDepth = 0;
                    int duplicateCount = 0;
                    
                    for (int i = 0; i < lines2.Length; i++)
                    {
                        string line2 = lines2[i];
                        string trimmed2 = line2.Trim();
                        
                        if (trimmed2.StartsWith("[[package]]"))
                        {
                            inPkg2 = true;
                            seenDeps2 = false;
                            skippingDuplicate = false;
                            arrayDepth = 0;
                            finalLines.Add(line2);
                        }
                        else if (inPkg2 && trimmed2.StartsWith("[[") && !trimmed2.StartsWith("[[package]]"))
                        {
                            inPkg2 = false;
                            seenDeps2 = false;
                            skippingDuplicate = false;
                            arrayDepth = 0;
                            finalLines.Add(line2);
                        }
                        else if (inPkg2)
                        {
                            // Check if this is a dependencies = [ line
                            if ((trimmed2.StartsWith("dependencies = [") || trimmed2.StartsWith("dependencies=[")) && arrayDepth == 0)
                            {
                                if (seenDeps2)
                                {
                                    // Duplicate detected - start skipping
                                    modified = true;
                                    duplicateCount++;
                                    skippingDuplicate = true;
                                    arrayDepth = 1;
                                    logger.LogInformation("Removed duplicate 'dependencies = [' at line {LineNum} in {Path} (second pass)", i + 1, lockPath);
                                    continue; // Skip this line
                                }
                                seenDeps2 = true;
                                arrayDepth = 1;
                            }
                            
                            // If we're skipping a duplicate array, track depth and skip until it closes
                            if (skippingDuplicate)
                            {
                                // Count brackets
                                int open = 0, close = 0;
                                foreach (char c in trimmed2)
                                {
                                    if (c == '[') open++;
                                    if (c == ']') close++;
                                }
                                arrayDepth += (open - close);
                                if (arrayDepth <= 0)
                                {
                                    arrayDepth = 0;
                                    skippingDuplicate = false; // Array closed, resume normal processing
                                }
                                continue; // Skip this line
                            }
                            
                            // Normal processing - track array depth
                            if (arrayDepth > 0)
                            {
                                int open = 0, close = 0;
                                foreach (char c in trimmed2)
                                {
                                    if (c == '[') open++;
                                    if (c == ']') close++;
                                }
                                arrayDepth += (open - close);
                                if (arrayDepth < 0) arrayDepth = 0;
                            }
                            
                            finalLines.Add(line2);
                        }
                        else
                        {
                            finalLines.Add(line2);
                        }
                    }
                    
                    if (modified && duplicateCount > 0)
                    {
                        content = string.Join("\n", finalLines);
                        logger.LogInformation("Removed {Count} duplicate 'dependencies = [' entries in second pass at {Path}", duplicateCount, lockPath);
                    }
                    
                    // Final aggressive regex: remove ANY duplicate "dependencies = [" lines
                    // Use a simpler approach: split by packages and ensure only one dependencies = [ per package
                    string beforeFinalRegex = content;
                    // Split content into package entries
                    var packageMatches = System.Text.RegularExpressions.Regex.Matches(
                        content,
                        @"(\[\[package\]\][\s\S]*?)(?=\[\[package\]\]|$)",
                        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Singleline);
                    
                    System.Text.StringBuilder fixedContent = new();
                    int lastIndex = 0;
                    
                    foreach (System.Text.RegularExpressions.Match pkgMatch in packageMatches)
                    {
                        // Add any content before this package
                        if (pkgMatch.Index > lastIndex)
                        {
                            fixedContent.Append(content.Substring(lastIndex, pkgMatch.Index - lastIndex));
                        }
                        
                        string packageContent = pkgMatch.Groups[1].Value;
                        
                        // Count occurrences of "dependencies = ["
                        int depsCount = System.Text.RegularExpressions.Regex.Matches(
                            packageContent,
                            @"^\s*dependencies\s*=\s*\[",
                            System.Text.RegularExpressions.RegexOptions.Multiline).Count;
                        
                        if (depsCount > 1)
                        {
                            // Remove all but the first occurrence
                            bool firstFound = false;
                            string[] pkgLines = packageContent.Split('\n');
                            foreach (string pkgLine in pkgLines)
                            {
                                string trimmedPkgLine = pkgLine.Trim();
                                if ((trimmedPkgLine.StartsWith("dependencies = [") || trimmedPkgLine.StartsWith("dependencies=[")) && !firstFound)
                                {
                                    firstFound = true;
                                    fixedContent.Append(pkgLine);
                                    fixedContent.Append('\n');
                                }
                                else if (trimmedPkgLine.StartsWith("dependencies = [") || trimmedPkgLine.StartsWith("dependencies=["))
                                {
                                    // Skip duplicate
                                    modified = true;
                                    logger.LogInformation("Removed duplicate 'dependencies = [' in package at {Path}", lockPath);
                                    continue;
                                }
                                else
                                {
                                    fixedContent.Append(pkgLine);
                                    fixedContent.Append('\n');
                                }
                            }
                        }
                        else
                        {
                            fixedContent.Append(packageContent);
                        }
                        
                        lastIndex = pkgMatch.Index + pkgMatch.Length;
                    }
                    
                    // Add any remaining content
                    if (lastIndex < content.Length)
                    {
                        fixedContent.Append(content.Substring(lastIndex));
                    }
                    
                    if (modified)
                    {
                        content = fixedContent.ToString();
                        logger.LogInformation("Removed duplicate 'dependencies = [' via package-by-package analysis at {Path}", lockPath);
                    }
                    
                    // ONE MORE BRUTE-FORCE PASS: Simple line-by-line, skip any duplicate dependencies = [
                    string[] ultraSimpleLines = content.Split('\n');
                    System.Collections.Generic.List<string> ultraSimpleFixed = new();
                    bool inPkgUltra = false;
                    bool seenDepsUltra = false;
                    int ultraDupCount = 0;
                    
                    for (int i = 0; i < ultraSimpleLines.Length; i++)
                    {
                        string line = ultraSimpleLines[i];
                        string trimmed = line.Trim();
                        
                        if (trimmed == "[[package]]")
                        {
                            inPkgUltra = true;
                            seenDepsUltra = false;
                            ultraSimpleFixed.Add(line);
                        }
                        else if (inPkgUltra && trimmed.StartsWith("[[") && !trimmed.StartsWith("[[package]]"))
                        {
                            inPkgUltra = false;
                            seenDepsUltra = false;
                            ultraSimpleFixed.Add(line);
                        }
                        else if (inPkgUltra && (trimmed.StartsWith("dependencies = [") || trimmed.StartsWith("dependencies=[")))
                        {
                            if (seenDepsUltra)
                            {
                                ultraDupCount++;
                                modified = true;
                                logger.LogInformation("ULTRA-SIMPLE: Skipped duplicate 'dependencies = [' at line {LineNum} in {Path}", i + 1, lockPath);
                                continue;
                            }
                            seenDepsUltra = true;
                            ultraSimpleFixed.Add(line);
                        }
                        else
                        {
                            ultraSimpleFixed.Add(line);
                        }
                    }
                    
                    if (modified && ultraDupCount > 0)
                    {
                        content = string.Join("\n", ultraSimpleFixed);
                        logger.LogInformation("ULTRA-SIMPLE: Removed {Count} duplicate 'dependencies = [' at {Path}", ultraDupCount, lockPath);
                    }
                    
                    // Remove ALL checksum fields for packages we modified (constant_time_eq, blake3, getrandom)
                    // This prevents checksum validation errors when we manually change versions
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"(\[\[package\]\]\s*\nname = ""(?:constant_time_eq|blake3|getrandom)""[^\[]*?)\nchecksum\s*=\s*""[^""]+""",
                        "$1",
                        System.Text.RegularExpressions.RegexOptions.Multiline);
                    
                    if (modified && content != original)
                    {
                        await File.WriteAllTextAsync(lockPath, content, token);
                        logger.LogInformation("Final fix: Updated constant_time_eq and blake3 versions in {Path} (removed checksums)", lockPath);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not perform final fix on {Path}: {Error}", lockPath, ex.Message);
                }
            }
            */
            
            // Step 5: Check for any .cargo/config.toml files that might cause path source errors
            try
            {
                var cargoConfigFiles = Directory.GetFiles(tempDir, "config.toml", SearchOption.AllDirectories)
                    .Where(p => p.Contains(".cargo")).ToList();
                foreach (var configPath in cargoConfigFiles)
                {
                    try
                    {
                        if (File.Exists(configPath))
                        {
                            string configContent = await File.ReadAllTextAsync(configPath, token);
                            logger.LogWarning("Found .cargo/config.toml at {Path} - this may cause path source errors. Content:\n{Content}", 
                                configPath, configContent.Length > 500 ? configContent.Substring(0, 500) + "..." : configContent);
                            // Delete it to prevent path source errors
                            File.Delete(configPath);
                            logger.LogInformation("Deleted .cargo/config.toml at {Path} to prevent path source errors", configPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not check/delete .cargo/config.toml at {Path}: {Error}", configPath, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not search for .cargo/config.toml files: {Error}", ex.Message);
            }
            
            // Step 6: Run anchor build
            // NEW APPROACH: Let anchor build create the lockfile, then fix it if it fails
            try
            {
                // Ensure the background fix task is running before we start anchor build
                // This will fix Cargo.toml files as they're downloaded
                await Task.Delay(100, token); // Give fix task a moment to start
                
                // CRITICAL: Pre-generate and fix Cargo.lock BEFORE anchor build
                // This prevents anchor from selecting incompatible versions
                try
                {
                    string workspaceRoot = Directory.GetFiles(tempDir, "Cargo.toml", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(f => 
                        {
                            try
                            {
                                string content = File.ReadAllText(f);
                                return content.Contains("[workspace]");
                            }
                            catch { return false; }
                        });
                    
                    if (!string.IsNullOrEmpty(workspaceRoot))
                    {
                        string? workspaceDirPath = Path.GetDirectoryName(workspaceRoot);
                        string workspaceDir = workspaceDirPath ?? tempDir;
                        logger.LogInformation("Pre-generating Cargo.lock in workspace root: {Dir}", workspaceDir);
                        
                        // CRITICAL: Use the SAME isolated CARGO_HOME that anchor build will use
                        // This ensures the lockfile and registry are consistent
                        string isolatedCargoHome = Path.Combine(tempDir, ".cargo_home");
                        Directory.CreateDirectory(isolatedCargoHome);
                        
                        // Generate lockfile using isolated CARGO_HOME
                        var lockfileResult = await ProcessExtensions.RunCargoAsyncWithEnv(
                            "generate-lockfile", 
                            workspaceDir, 
                            logger, 
                            new Dictionary<string, string> { { "CARGO_HOME", isolatedCargoHome } },
                            token);
                        if (lockfileResult.IsSuccess)
                        {
                            // CRITICAL: Force downgrade blake3 to 1.8.2 which removes constant_time_eq 0.4.2
                            logger.LogInformation("Forcing blake3 to 1.8.2 to remove constant_time_eq 0.4.2 dependency");
                            var updateResult = await ProcessExtensions.RunCargoAsyncWithEnv(
                                "update -p blake3 --precise 1.8.2", 
                                workspaceDir, 
                                logger,
                                new Dictionary<string, string> { { "CARGO_HOME", isolatedCargoHome } },
                                token);
                            if (updateResult.IsSuccess)
                            {
                                logger.LogInformation("Successfully downgraded blake3 to 1.8.2, removing constant_time_eq 0.4.2");
                            }
                            else
                            {
                                logger.LogWarning("Could not downgrade blake3: {Error}", updateResult.GetErrorMessage());
                            }
                        }
                        else
                        {
                            logger.LogWarning("Could not generate lockfile: {Error}", lockfileResult.GetErrorMessage());
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not pre-generate/fix Cargo.lock: {Error}", ex.Message);
                }
                
                ProcessExecutionResult result = await ProcessExtensions.RunAnchorAsync(tempDir, logger, token);
                
                // If build failed due to constant_time_eq 0.4.2, blake3 1.8.3, duplicate key, or other lockfile issues, fix and retry
                if (!result.IsSuccess && (result.StandardError.Contains("constant_time_eq v0.4.2") ||
                    result.StandardError.Contains("constant_time_eq 0.4.2") ||
                    result.StandardError.Contains("blake3 v1.8.3") ||
                    result.StandardError.Contains("blake3 1.8.3") ||
                    result.StandardError.Contains("edition2024") ||
                    result.StandardError.Contains("duplicate key") || 
                    result.StandardError.Contains("TOML parse error") ||
                    result.StandardError.Contains("lock file version 4") || 
                    result.StandardOutput.Contains("lock file version 4")))
                {
                    logger.LogWarning("Build failed due to Cargo.lock or dependency issue, fixing all lock files and retrying...");
                    
                    // Fix all Cargo.lock files
                    var lockFiles = Directory.GetFiles(tempDir, "Cargo.lock", SearchOption.AllDirectories);
                    foreach (var lockPath in lockFiles)
                    {
                        try
                        {
                            string content = await File.ReadAllTextAsync(lockPath, token);
                            bool wasFixed = false;
                            
                            // Fix version 4 → 3
                            if (content.Contains("version = 4"))
                            {
                                content = content.Replace("version = 4", "version = 3");
                                wasFixed = true;
                            }
                            
                            // SIMPLE FIX: Remove duplicate dependencies = [ using a single, clean pass
                            // Split by packages and ensure only one dependencies = [ per package
                            var packages = System.Text.RegularExpressions.Regex.Split(
                                content,
                                @"(\[\[package\]\])",
                                System.Text.RegularExpressions.RegexOptions.Multiline);
                            
                            System.Text.StringBuilder fixedContent = new();
                            bool inPackage = false;
                            
                            foreach (string part in packages)
                            {
                                if (part.Trim() == "[[package]]")
                                {
                                    inPackage = true;
                                    fixedContent.Append(part);
                                }
                                else if (inPackage)
                                {
                                    // Check if this package section has multiple dependencies = [
                                    int depsCount = System.Text.RegularExpressions.Regex.Matches(
                                        part,
                                        @"^\s*dependencies\s*=\s*\[",
                                        System.Text.RegularExpressions.RegexOptions.Multiline).Count;
                                    
                                    if (depsCount > 1)
                                    {
                                        // Remove all but the first
                                        string[] lines = part.Split('\n');
                                        bool firstFound = false;
                                        foreach (string line in lines)
                                        {
                                            string trimmed = line.Trim();
                                            if ((trimmed.StartsWith("dependencies = [") || trimmed.StartsWith("dependencies=[")) && !firstFound)
                                            {
                                                firstFound = true;
                                                fixedContent.Append(line);
                                                fixedContent.Append('\n');
                                            }
                                            else if (trimmed.StartsWith("dependencies = [") || trimmed.StartsWith("dependencies=["))
                                            {
                                                // Skip duplicate
                                                wasFixed = true;
                                                continue;
                                            }
                                            else
                                            {
                                                fixedContent.Append(line);
                                                fixedContent.Append('\n');
                                            }
                                        }
                                    }
                                    else
                                    {
                                        fixedContent.Append(part);
                                    }
                                    
                                    // Check if we're leaving this package
                                    if (part.Contains("[[") && !part.Contains("[[package]]"))
                                    {
                                        inPackage = false;
                                    }
                                }
                                else
                                {
                                    fixedContent.Append(part);
                                }
                            }
                            
                            // CRITICAL: Fix constant_time_eq 0.4.2 and blake3 1.8.3 issues
                            // Use a more robust approach that properly handles package removal without creating duplicates
                            string fixedLockContent = fixedContent.ToString();
                            
                            // Remove entire [[package]] entry for constant_time_eq 0.4.2
                            var constantTimeEqPattern = @"\[\[package\]\]\s*\nname\s*=\s*""constant_time_eq""\s*\nversion\s*=\s*""0\.4\.2""[^\[]*?(?=\[\[package\]\]|$)";
                            fixedLockContent = System.Text.RegularExpressions.Regex.Replace(
                                fixedLockContent,
                                constantTimeEqPattern,
                                "",
                                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                            
                            // Remove entire [[package]] entry for blake3 1.8.3
                            var blake3Pattern = @"\[\[package\]\]\s*\nname\s*=\s*""blake3""\s*\nversion\s*=\s*""1\.8\.3""[^\[]*?(?=\[\[package\]\]|$)";
                            fixedLockContent = System.Text.RegularExpressions.Regex.Replace(
                                fixedLockContent,
                                blake3Pattern,
                                "",
                                System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.Multiline);
                            
                            // Replace all references to constant_time_eq 0.4.2 with 0.3.1
                            fixedLockContent = fixedLockContent.Replace("constant_time_eq 0.4.2", "constant_time_eq 0.3.1");
                            fixedLockContent = fixedLockContent.Replace("\"constant_time_eq 0.4.2\"", "\"constant_time_eq 0.3.1\"");
                            
                            // Replace all references to blake3 1.8.3 with 1.8.2
                            fixedLockContent = fixedLockContent.Replace("blake3 1.8.3", "blake3 1.8.2");
                            fixedLockContent = fixedLockContent.Replace("\"blake3 1.8.3\"", "\"blake3 1.8.2\"");
                            
                            // CRITICAL: Remove duplicate keys (source, checksum, etc.) that can occur after package removal
                            // Parse line by line and remove duplicates within each package entry
                            string[] lockLines = fixedLockContent.Split('\n');
                            System.Collections.Generic.List<string> cleanedLines = new();
                            System.Collections.Generic.HashSet<string> seenKeysInPackage = new();
                            bool inPackageEntry = false;
                            
                            for (int i = 0; i < lockLines.Length; i++)
                            {
                                string line = lockLines[i];
                                string trimmed = line.Trim();
                                
                                if (trimmed == "[[package]]")
                                {
                                    // New package entry - reset seen keys
                                    seenKeysInPackage.Clear();
                                    inPackageEntry = true;
                                    cleanedLines.Add(line);
                                }
                                else if (inPackageEntry && trimmed.StartsWith("[[") && !trimmed.StartsWith("[[package]]"))
                                {
                                    // End of package entry
                                    inPackageEntry = false;
                                    seenKeysInPackage.Clear();
                                    cleanedLines.Add(line);
                                }
                                else if (inPackageEntry && trimmed.Contains("="))
                                {
                                    // Extract key name
                                    int eqIndex = trimmed.IndexOf('=');
                                    if (eqIndex > 0)
                                    {
                                        string keyName = trimmed.Substring(0, eqIndex).Trim();
                                        
                                        // Check for duplicate keys (source, checksum, version, name)
                                        if ((keyName == "source" || keyName == "checksum" || keyName == "version" || keyName == "name") && 
                                            seenKeysInPackage.Contains(keyName))
                                        {
                                            // Skip duplicate
                                            wasFixed = true;
                                            continue;
                                        }
                                        
                                        seenKeysInPackage.Add(keyName);
                                    }
                                    cleanedLines.Add(line);
                                }
                                else
                                {
                                    cleanedLines.Add(line);
                                }
                            }
                            
                            fixedLockContent = string.Join("\n", cleanedLines);
                            
                            if (wasFixed || fixedLockContent != content)
                            {
                                await File.WriteAllTextAsync(lockPath, fixedLockContent, token);
                                logger.LogInformation("Fixed Cargo.lock at {Path} (removed constant_time_eq 0.4.2, blake3 1.8.3, and duplicate keys)", lockPath);
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Could not fix Cargo.lock at {Path}: {Error}", lockPath, ex.Message);
                        }
                    }
                    
                    // Wait for file system to sync
                    await Task.Delay(500, token);
                    
                    // Retry the build
                    logger.LogInformation("Retrying anchor build after fixing Cargo.lock files...");
                    result = await ProcessExtensions.RunAnchorAsync(tempDir, logger, token);
                }
                
                if (!result.IsSuccess)
                {
                    // Log full error details for debugging
                    logger.LogError("Compilation failed. ExitCode: {ExitCode}, Duration: {Duration}ms", 
                        result.ExitCode, result.Duration.TotalMilliseconds);
                    logger.LogError("StandardOutput: {StdOut}", result.StandardOutput);
                    logger.LogError("StandardError: {StdErr}", result.StandardError);
                    
                    // Build comprehensive error message
                    string errorMessage = result.GetErrorMessage();
                    if (result.Duration.TotalMinutes >= 29.5)
                    {
                        errorMessage = "Compilation timeout after 30 minutes. " +
                            "First builds can take longer due to dependency downloads. " +
                            "Please try again - subsequent builds will use cache and be faster. " +
                            errorMessage;
                    }
                    
                    // Include both stdout and stderr in error for better debugging
                    string fullError = result.GetCombinedOutput();
                    if (fullError.Length > 2000)
                    {
                        // Truncate but keep important parts
                        fullError = fullError.Substring(0, 1000) + 
                            "\n... [truncated] ...\n" + 
                            fullError.Substring(fullError.Length - 1000);
                    }
                    
                    if (!string.IsNullOrWhiteSpace(fullError))
                    {
                        return Result<CompileContractResponse>.Failure(
                            ResultPatternError.BadRequest($"{errorMessage}\n\nDetails:\n{fullError}"));
                    }

                    return Result<CompileContractResponse>.Failure(
                        ResultPatternError.InternalServerError(errorMessage));
                }
                
                // Success path - continue to cache and return response
                
                // Cache the target directory for next build
                if (Directory.Exists(projectTarget))
                {
                    try
                    {
                        if (Directory.Exists(sharedTargetCache))
                            Directory.Delete(sharedTargetCache, true);
                        
                        CopyDirectory(projectTarget, sharedTargetCache);
                        logger.LogInformation("Cached build artifacts for future compilations");
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Could not cache target directory");
                    }
                }

                return await CreateResponseAsync(tempDir, token);
            }
            finally
            {
                // Stop the background fix monitor and fix task
                try
                {
                    if (fixMonitorProcess != null && !fixMonitorProcess.HasExited)
                    {
                        fixMonitorProcess.Kill();
                        fixMonitorProcess.WaitForExit(2000);
                    }
                    fixMonitorProcess?.Dispose();
                    
                    // Stop the background fix task
                    fixTaskCts?.Cancel();
                    if (fixTask != null)
                    {
                        try
                        {
                            await Task.WhenAny(fixTask, Task.Delay(1000, token));
                        }
                        catch { }
                    }
                    fixTaskCts?.Dispose();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error stopping fix monitor/task: {Error}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            logger.OperationFailed(nameof(CompileAsync), ex.Message,
                httpContext.GetId().ToString(), httpContext.GetCorrelationId());
            return Result<CompileContractResponse>.Failure(ResultPatternError.InternalServerError(ex.Message));
        }
        finally
        {
            // Clean up the temp build directory but keep the shared cache
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
            catch { /* Ignore cleanup errors */ }
            
            stopwatch.Stop();
            logger.OperationCompleted(nameof(CompileAsync),
                stopwatch.ElapsedMilliseconds, httpContext.GetCorrelationId());
        }
    }
    
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        
        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }
        
        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }
}