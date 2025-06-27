[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Name,
    
    [Parameter(Mandatory = $false)]
    [string]$StartupProject = ".\src\IntelliTrans.Cli\IntelliTrans.Cli.csproj"
)

$ErrorActionPreference = "Stop"
$originalLocation = Get-Location
$scriptDir = $PSScriptRoot

try {
    Set-Location $scriptDir
    
    Write-Host "Building project..." -ForegroundColor Cyan
    dotnet build
    
    # 获取所有migration项目
    $migrationProjects = Get-ChildItem -Path ".\src" -Filter "IntelliTrans.Migrations.*" -Directory
    
    if (-not $migrationProjects) {
        Write-Warning "未找到任何迁移项目！"
        return
    }
    
    foreach ($project in $migrationProjects) {
        # 从目录名称提取provider名称
        $provider = $project.Name -replace "IntelliTrans.Migrations.", ""
        
        # 构建完整的项目路径
        $projectPath = Join-Path $project.FullName "$($project.Name).csproj"
        
        if (-not (Test-Path $projectPath)) {
            Write-Warning "项目文件不存在: $projectPath"
            continue
        }
        
        Write-Host "为 $provider 提供程序添加迁移..." -ForegroundColor Green
        
        # 设置环境变量
        $env:DBTYPE = $provider
        Write-Host "设置环境变量 DBTYPE=$provider" -ForegroundColor Yellow
        
        # 运行dotnet ef命令，不再使用命令行参数传递provider
        $command = "dotnet ef migrations add $Name --no-build --project `"$projectPath`" --startup-project `"$StartupProject`""
        Write-Host "执行: $command" -ForegroundColor Yellow
        
        try {
            Invoke-Expression $command
            if ($LASTEXITCODE -ne 0) {
                Write-Error "添加迁移到 $provider 时出错。退出代码: $LASTEXITCODE"
            }
        }
        catch {
            Write-Error "添加迁移到 $provider 时出错: $_"
        }
        finally {
            # 清除环境变量
            Remove-Item Env:\DBTYPE -ErrorAction SilentlyContinue
        }
    }
    
    Write-Host "所有提供程序的迁移添加成功！" -ForegroundColor Green
}
catch {
    Write-Error "执行脚本时出错: $_"
}
finally {
    # 确保环境变量被清除
    Remove-Item Env:\DBTYPE -ErrorAction SilentlyContinue
    Set-Location $originalLocation
}