Import-Module (Join-Path $PSScriptRoot "PoshGit.dll")

Push-Location $psScriptRoot
.\CheckVersion.ps1 > $null

. ./Utils.ps1
. ./GitUtils.ps1
. ./GitPrompt.ps1
. ./GitTabExpansion.ps1
. ./TortoiseGit.ps1
Pop-Location

Export-ModuleMember -function Write-GitStatus,
        Get-GitStatus,
        Enable-GitColors, 
        Get-GitDirectory,
        GitTabExpansion,
        Get-GitAliasPattern,
        Start-SshAgent,
        tgit -alias 'gitstat'
