#Requires -Version 7.0
param(
    [uri]$Api = 'http://localhost:5090',
    [pscredential]$Credential
)

$ErrorActionPreference = 'Stop'

if (!$Api.IsAbsoluteUri -or $Api.Scheme -notin 'http', 'https' -or $Api.AbsolutePath -ne '/' -or
    $Api.UserInfo -or $Api.Query -or $Api.Fragment) {
    throw 'Podaj adres bazowy aplikacji, np. http://localhost:5090.'
}

$baseUrl = $Api.GetLeftPart([UriPartial]::Authority)
$project = Join-Path $PSScriptRoot '../tools/PitchPulse.SensorEmulator/PitchPulse.SensorEmulator.csproj'
dotnet build $project --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw 'Kompilacja emulatora nie powiodła się.'
}

if (!$Credential) {
    $Credential = Get-Credential -Message 'Podaj dane administratora PitchPulse.'
}

$http = @{ MaximumRedirection = 0; TimeoutSec = 15 }
$loginBody = @{ username = $Credential.UserName; password = $Credential.GetNetworkCredential().Password }
$login = Invoke-RestMethod @http -Uri "$baseUrl/api/auth/login" -Method Post `
    -ContentType 'application/json; charset=utf-8' -Body ($loginBody | ConvertTo-Json -Compress)
$admin = @{ Authorization = "Bearer $($login.accessToken)" }
$savedKey = $env:PITCHPULSE_SENSOR_KEY
$start = [DateTimeOffset]::UtcNow.AddMinutes(-35).ToString('O')
$culture = [Globalization.CultureInfo]::InvariantCulture

$players = @(
    @{ Id = 1; Number = 7; Color = '#1f77b4'; Icon = 'circle'; Min = 112; Max = 164 },
    @{ Id = 2; Number = 9; Color = '#ff7f0e'; Icon = 'square'; Min = 120; Max = 174 },
    @{ Id = 3; Number = 10; Color = '#2ca02c'; Icon = 'diamond'; Min = 125; Max = 185 }
)

try {
    $series = Invoke-RestMethod @http -Uri "$baseUrl/api/series"
    foreach ($player in $players) {
        $target = $series | Where-Object { $_.playerId -eq $player.Id -and $_.metricType -eq 'HeartRate' } | Select-Object -First 1
        if (!$target) {
            $body = @{
                name = "Tętno — zawodnik nr $($player.Number)"
                playerId = $player.Id
                metricType = 'HeartRate'
                minValue = 40
                maxValue = 220
                unit = 'bpm'
                color = $player.Color
                icon = $player.Icon
            }
            $target = Invoke-RestMethod @http -Uri "$baseUrl/api/series" -Method Post -Headers $admin `
                -ContentType 'application/json; charset=utf-8' -Body ($body | ConvertTo-Json -Compress)
        }

        $min = [Math]::Max([double]$target.minValue, [double]$player.Min)
        $max = [Math]::Min([double]$target.maxValue, [double]$player.Max)
        if ($min -ge $max) {
            $min = [double]$target.minValue
            $max = [double]$target.maxValue
        }

        $body = @{ name = "Emulator demo nr $($player.Number)"; seriesId = $target.id }
        $sensor = Invoke-RestMethod @http -Uri "$baseUrl/api/sensors" -Method Post -Headers $admin `
            -ContentType 'application/json; charset=utf-8' -Body ($body | ConvertTo-Json -Compress)

        try {
            Write-Host "Wysyłanie danych do serii $($target.name)"
            $env:PITCHPULSE_SENSOR_KEY = $sensor.apiKey
            dotnet run --no-build --project $project -- --api $baseUrl --mode history --start $start --count 60 `
                --interval-seconds 30 --pattern sine --min ($min.ToString($culture)) --max ($max.ToString($culture))
            if ($LASTEXITCODE -ne 0) {
                throw "Emulator zakończył się błędem dla serii $($target.id)."
            }
        }
        finally {
            $env:PITCHPULSE_SENSOR_KEY = $savedKey
            try {
                Invoke-RestMethod @http -Uri "$baseUrl/api/sensors/$($sensor.id)" -Method Delete -Headers $admin | Out-Null
            }
            catch {
                Write-Warning "Nie udało się wyrejestrować tymczasowego czujnika $($sensor.id)."
            }
        }
    }
}
finally {
    try {
        Invoke-RestMethod @http -Uri "$baseUrl/api/auth/logout" -Method Post -Headers $admin | Out-Null
    }
    catch {
        Write-Warning 'Nie udało się wylogować konta użytego do demonstracji.'
    }
}
