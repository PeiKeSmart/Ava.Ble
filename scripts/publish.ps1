# JieLi OTA 发布脚本
# PowerShell 脚本用于构建和打包发布版本

param(
    [Parameter(Mandatory=$false)]
    [string]$Version = "1.0.0",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$SkipTests,
    
    [Parameter(Mandatory=$false)]
    [switch]$CreateZip
)

$ErrorActionPreference = "Stop"

# 颜色输出函数
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

# 检查工具
function Test-Tool {
    param([string]$Tool)
    
    $exists = Get-Command $Tool -ErrorAction SilentlyContinue
    if (-not $exists) {
        Write-ColorOutput "错误: 未找到 $Tool,请先安装" "Red"
        exit 1
    }
}

Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "  JieLi OTA 发布脚本" "Cyan"
Write-ColorOutput "  版本: $Version" "Cyan"
Write-ColorOutput "  配置: $Configuration" "Cyan"
Write-ColorOutput "========================================" "Cyan"
Write-Host ""

# 检查必需工具
Write-ColorOutput "检查必需工具..." "Yellow"
Test-Tool "dotnet"
Test-Tool "git"
Write-ColorOutput "✓ 工具检查完成" "Green"
Write-Host ""

# 项目路径
$SolutionFile = "JieLi.OTA.sln"
$DesktopProject = "src/JieLi.OTA.Desktop/JieLi.OTA.Desktop.csproj"
$OutputDir = "publish"

# 清理旧的发布文件
Write-ColorOutput "清理旧的发布文件..." "Yellow"
if (Test-Path $OutputDir) {
    Remove-Item -Path $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null
Write-ColorOutput "✓ 清理完成" "Green"
Write-Host ""

# 运行测试
if (-not $SkipTests) {
    Write-ColorOutput "运行单元测试..." "Yellow"
    $testResult = dotnet test $SolutionFile --verbosity quiet
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "✗ 测试失败!" "Red"
        exit 1
    }
    Write-ColorOutput "✓ 所有测试通过" "Green"
    Write-Host ""
}

# 发布配置
$publishConfigs = @(
    @{
        Name = "win-x64-self-contained"
        DisplayName = "Windows x64 (自包含)"
        Runtime = "win-x64"
        SelfContained = $true
        SingleFile = $true
    },
    @{
        Name = "win-x64-framework-dependent"
        DisplayName = "Windows x64 (依赖框架)"
        Runtime = "win-x64"
        SelfContained = $false
        SingleFile = $false
    }
)

# 发布每个配置
foreach ($config in $publishConfigs) {
    Write-ColorOutput "发布 $($config.DisplayName)..." "Yellow"
    
    $publishDir = Join-Path $OutputDir $config.Name
    
    $args = @(
        "publish",
        $DesktopProject,
        "-c", $Configuration,
        "-r", $config.Runtime,
        "--self-contained", $config.SelfContained,
        "-o", $publishDir,
        "/p:Version=$Version",
        "/p:AssemblyVersion=$Version.0",
        "/p:FileVersion=$Version.0"
    )
    
    if ($config.SingleFile) {
        $args += "/p:PublishSingleFile=true"
        $args += "/p:IncludeNativeLibrariesForSelfExtract=true"
        $args += "/p:EnableCompressionInSingleFile=true"
    }
    
    & dotnet $args
    
    if ($LASTEXITCODE -ne 0) {
        Write-ColorOutput "✗ 发布失败: $($config.DisplayName)" "Red"
        exit 1
    }
    
    Write-ColorOutput "✓ 发布完成: $($config.DisplayName)" "Green"
    
    # 创建 ZIP 包
    if ($CreateZip) {
        $zipName = "JieLi.OTA.v$Version-$($config.Name).zip"
        $zipPath = Join-Path $OutputDir $zipName
        
        Write-ColorOutput "  创建 ZIP 包: $zipName" "Cyan"
        Compress-Archive -Path "$publishDir/*" -DestinationPath $zipPath -Force
        Write-ColorOutput "  ✓ ZIP 创建完成" "Green"
    }
    
    Write-Host ""
}

# 显示发布信息
Write-ColorOutput "========================================" "Cyan"
Write-ColorOutput "  发布完成!" "Green"
Write-ColorOutput "========================================" "Cyan"
Write-Host ""
Write-ColorOutput "发布文件位置: $OutputDir" "White"
Get-ChildItem -Path $OutputDir -Directory | ForEach-Object {
    Write-ColorOutput "  - $($_.Name)" "Gray"
}

if ($CreateZip) {
    Write-Host ""
    Write-ColorOutput "ZIP 包:" "White"
    Get-ChildItem -Path $OutputDir -Filter "*.zip" | ForEach-Object {
        $sizeMB = [math]::Round($_.Length / 1MB, 2)
        Write-ColorOutput "  - $($_.Name) ($sizeMB MB)" "Gray"
    }
}

Write-Host ""
Write-ColorOutput "后续步骤:" "Yellow"
Write-ColorOutput "  1. 测试发布的应用程序" "White"
Write-ColorOutput "  2. 创建 Git 标签: git tag -a v$Version -m 'Release version $Version'" "White"
Write-ColorOutput "  3. 推送标签: git push origin v$Version" "White"
Write-ColorOutput "  4. 在 GitHub 创建 Release" "White"
Write-Host ""
