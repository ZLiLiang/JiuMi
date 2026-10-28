# Generates the bundled world clock catalog from CLDR, IANA tzdb and libphonenumber.
#
# Development only: the script is not part of the build, is not run by CI, and the
# application never reaches the network for catalog data. The generated JSON files
# are committed under src/JiuMi.Application/Assets/world-clock.
#
#   pwsh ./tools/generate-world-clock-data.ps1
#   pwsh ./tools/generate-world-clock-data.ps1 -Refresh   # ignore the cached downloads

[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\src\JiuMi.Application\Assets\world-clock'),
    [switch]$Refresh
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$cacheDirectory = Join-Path $env:TEMP 'jiumi-world-clock-data'
if ($Refresh -and (Test-Path $cacheDirectory)) {
    Remove-Item $cacheDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $cacheDirectory | Out-Null

$utf8 = [System.Text.UTF8Encoding]::new($false)

function Get-Source {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Uri
    )

    $target = Join-Path $cacheDirectory $Name
    if (-not (Test-Path $target)) {
        Write-Host "Downloading $Uri"
        $temp = "$target.tmp"
        $attempt = 0
        while ($true) {
            $attempt++
            try {
                Invoke-WebRequest -Uri $Uri -OutFile $temp -UseBasicParsing -TimeoutSec 60
                Move-Item -LiteralPath $temp -Destination $target -Force
                break
            }
            catch {
                Remove-Item -LiteralPath $temp -Force -ErrorAction SilentlyContinue
                if ($attempt -ge 3) {
                    throw
                }

                Write-Warning "Download failed (attempt $attempt): $($_.Exception.Message)"
                Start-Sleep -Seconds 2
            }
        }
    }

    return $target
}

# Names and versions -------------------------------------------------------------

$territoriesPath = Get-Source -Name 'territories-zh-Hans.json' -Uri 'https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-localenames-full/main/zh-Hans/territories.json'
$territoriesEnPath = Get-Source -Name 'territories-en.json' -Uri 'https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-localenames-full/main/en/territories.json'
$containmentPath = Get-Source -Name 'territoryContainment.json' -Uri 'https://raw.githubusercontent.com/unicode-org/cldr-json/main/cldr-json/cldr-core/supplemental/territoryContainment.json'
$zonesPath = Get-Source -Name 'zone1970.tab' -Uri 'https://raw.githubusercontent.com/eggert/tz/main/zone1970.tab'
$linksPath = Get-Source -Name 'backward' -Uri 'https://raw.githubusercontent.com/eggert/tz/main/backward'
$tzdbVersionPath = Get-Source -Name 'tzdb-version' -Uri 'https://data.iana.org/time-zones/tzdb/version'
$phonePath = Get-Source -Name 'PhoneNumberMetadata.xml' -Uri 'https://raw.githubusercontent.com/google/libphonenumber/master/resources/PhoneNumberMetadata.xml'
$phoneCommitPath = Get-Source -Name 'libphonenumber-commit.json' -Uri 'https://api.github.com/repos/google/libphonenumber/commits/master'

$zhTerritories = (Get-Content $territoriesPath -Raw | ConvertFrom-Json).main.'zh-Hans'.localeDisplayNames.territories
$enTerritories = (Get-Content $territoriesEnPath -Raw | ConvertFrom-Json).main.en.localeDisplayNames.territories
$cldrVersion = (Get-Content $containmentPath -Raw | ConvertFrom-Json).supplemental.version
$tzdbVersion = (Get-Content $tzdbVersionPath -Raw).Trim()
$phoneCommit = (Get-Content $phoneCommitPath -Raw | ConvertFrom-Json).sha

function Get-TerritoryName {
    param($Table, [string]$Code, [string]$Suffix)

    $key = if ($Suffix) { "$Code$Suffix" } else { $Code }
    $property = $Table.PSObject.Properties[$key]
    if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        return [string]$property.Value
    }

    return $null
}

function Get-ShortName {
    param($Table, [string]$Code)

    $short = Get-TerritoryName -Table $Table -Code $Code -Suffix '-alt-short'

    # CLDR also shortens "United States" to "US" and "United Kingdom" to "UK"; those read as
    # abbreviations next to a dial code, so keep the standard name in that case.
    if ($null -ne $short -and $short -notmatch '^[A-Z]{2,3}$') {
        return $short
    }

    return Get-TerritoryName -Table $Table -Code $Code -Suffix ''
}

# Zones, links and phone codes ---------------------------------------------------

$zonesByCountry = @{}
$knownZones = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

foreach ($line in Get-Content $zonesPath) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
        continue
    }

    $columns = $line -split "`t"
    $zone = $columns[2]
    [void]$knownZones.Add($zone)
    foreach ($code in $columns[0] -split ',') {
        if (-not $zonesByCountry.ContainsKey($code)) {
            $zonesByCountry[$code] = [System.Collections.Generic.List[string]]::new()
        }

        $zonesByCountry[$code].Add($zone)
    }
}

foreach ($line in Get-Content $linksPath) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
        continue
    }

    $columns = @($line -split "`t" | Where-Object { $_ -ne '' })
    if ($columns.Count -ge 3 -and $columns[0] -eq 'Link') {
        [void]$knownZones.Add($columns[2])
    }
}

[xml]$phoneDocument = Get-Content $phonePath -Raw
$phoneCodes = @{}
foreach ($territory in $phoneDocument.DocumentElement.territories.territory) {
    $phoneCodes[$territory.GetAttribute('id')] = $territory.GetAttribute('countryCode')
}

# Representative zones for multi-zone countries. The city is shown in the row for
# these countries only; single-zone countries use their only zone and no city.
$representatives = @{
    AR = @{ zone = 'America/Argentina/Buenos_Aires'; zh = '布宜诺斯艾利斯'; en = 'Buenos Aires' }
    AU = @{ zone = 'Australia/Sydney'; zh = '悉尼'; en = 'Sydney' }
    BR = @{ zone = 'America/Sao_Paulo'; zh = '圣保罗'; en = 'Sao Paulo' }
    CA = @{ zone = 'America/Toronto'; zh = '多伦多'; en = 'Toronto' }
    CD = @{ zone = 'Africa/Kinshasa'; zh = '金沙萨'; en = 'Kinshasa' }
    CL = @{ zone = 'America/Santiago'; zh = '圣地亚哥'; en = 'Santiago' }
    CN = @{ zone = 'Asia/Shanghai'; zh = '上海'; en = 'Shanghai' }
    CY = @{ zone = 'Asia/Nicosia'; zh = '尼科西亚'; en = 'Nicosia' }
    DE = @{ zone = 'Europe/Berlin'; zh = '柏林'; en = 'Berlin' }
    EC = @{ zone = 'America/Guayaquil'; zh = '瓜亚基尔'; en = 'Guayaquil' }
    ES = @{ zone = 'Europe/Madrid'; zh = '马德里'; en = 'Madrid' }
    FM = @{ zone = 'Pacific/Pohnpei'; zh = '帕利基尔'; en = 'Palikir' }
    GL = @{ zone = 'America/Nuuk'; zh = '努克'; en = 'Nuuk' }
    ID = @{ zone = 'Asia/Jakarta'; zh = '雅加达'; en = 'Jakarta' }
    KI = @{ zone = 'Pacific/Tarawa'; zh = '塔拉瓦'; en = 'Tarawa' }
    KZ = @{ zone = 'Asia/Almaty'; zh = '阿拉木图'; en = 'Almaty' }
    MH = @{ zone = 'Pacific/Majuro'; zh = '马朱罗'; en = 'Majuro' }
    MN = @{ zone = 'Asia/Ulaanbaatar'; zh = '乌兰巴托'; en = 'Ulaanbaatar' }
    MX = @{ zone = 'America/Mexico_City'; zh = '墨西哥城'; en = 'Mexico City' }
    MY = @{ zone = 'Asia/Kuala_Lumpur'; zh = '吉隆坡'; en = 'Kuala Lumpur' }
    NZ = @{ zone = 'Pacific/Auckland'; zh = '奥克兰'; en = 'Auckland' }
    PF = @{ zone = 'Pacific/Tahiti'; zh = '帕皮提'; en = 'Papeete' }
    PG = @{ zone = 'Pacific/Port_Moresby'; zh = '莫尔兹比港'; en = 'Port Moresby' }
    PS = @{ zone = 'Asia/Gaza'; zh = '加沙'; en = 'Gaza' }
    PT = @{ zone = 'Europe/Lisbon'; zh = '里斯本'; en = 'Lisbon' }
    RU = @{ zone = 'Europe/Moscow'; zh = '莫斯科'; en = 'Moscow' }
    # Windows resolves this zone through its older IANA link only; the wall clock is identical.
    UA = @{ zone = 'Europe/Kiev'; zh = '基辅'; en = 'Kyiv' }
    US = @{ zone = 'America/New_York'; zh = '纽约'; en = 'New York' }
    UZ = @{ zone = 'Asia/Tashkent'; zh = '塔什干'; en = 'Tashkent' }
    VN = @{ zone = 'Asia/Ho_Chi_Minh'; zh = '胡志明市'; en = 'Ho Chi Minh City' }
}

# Countries whose territory spans different UTC offsets. Only these append a representative
# city to the row name; the others use their single offset even if tzdb links more zones.
$multiZoneCountries = @(
    'AR', 'AU', 'BR', 'CA', 'CD', 'CL', 'EC', 'ES', 'FM', 'GL', 'ID', 'KI', 'KZ', 'MH',
    'MN', 'MX', 'NZ', 'PF', 'PG', 'PT', 'RU', 'UA', 'US'
)

$countries = [System.Collections.Generic.List[object]]::new()
$countryCodes = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($code in ($zonesByCountry.Keys | Sort-Object)) {
    if (-not $phoneCodes.ContainsKey($code) -or $code.Length -ne 2) {
        continue
    }

    $zhName = Get-ShortName -Table $zhTerritories -Code $code
    $enName = Get-ShortName -Table $enTerritories -Code $code
    if ($null -eq $zhName -or $null -eq $enName) {
        continue
    }

    $countryZones = $zonesByCountry[$code]
    $entry = [ordered]@{
        iso2 = $code
        name = [ordered]@{ zh = $zhName; en = $enName }
        dialCode = "+$($phoneCodes[$code])"
    }

    $zone = $null
    $city = $null
    if ($representatives.ContainsKey($code)) {
        $representative = $representatives[$code]
        if (-not $knownZones.Contains($representative.zone)) {
            throw "Representative zone $($representative.zone) of $code is not an IANA id."
        }

        $zone = $representative.zone
        if ($multiZoneCountries -contains $code) {
            $city = [ordered]@{ zh = $representative.zh; en = $representative.en }
        }
    }
    elseif ($countryZones.Count -eq 1) {
        $zone = $countryZones[0]
    }
    else {
        throw "Multi-zone country $code has no representative zone."
    }

    $entry.representative = [ordered]@{ zone = $zone }
    if ($null -ne $city) {
        $entry.representative.city = $city
    }

    $countries.Add($entry)
    [void]$countryCodes.Add($code)
}

# Curated business cities. One city per IANA zone; the city id is the zone id.
$cityCatalog = @(
    @{ zone = 'Asia/Shanghai'; country = 'CN'; zh = '上海'; en = 'Shanghai' }
    @{ zone = 'Asia/Hong_Kong'; country = 'HK'; zh = '香港'; en = 'Hong Kong' }
    @{ zone = 'Asia/Macau'; country = 'MO'; zh = '澳门'; en = 'Macao' }
    @{ zone = 'Asia/Taipei'; country = 'TW'; zh = '台北'; en = 'Taipei' }
    @{ zone = 'Asia/Tokyo'; country = 'JP'; zh = '东京'; en = 'Tokyo' }
    @{ zone = 'Asia/Seoul'; country = 'KR'; zh = '首尔'; en = 'Seoul' }
    @{ zone = 'Asia/Singapore'; country = 'SG'; zh = '新加坡'; en = 'Singapore' }
    @{ zone = 'Asia/Kuala_Lumpur'; country = 'MY'; zh = '吉隆坡'; en = 'Kuala Lumpur' }
    @{ zone = 'Asia/Bangkok'; country = 'TH'; zh = '曼谷'; en = 'Bangkok' }
    @{ zone = 'Asia/Ho_Chi_Minh'; country = 'VN'; zh = '胡志明市'; en = 'Ho Chi Minh City' }
    @{ zone = 'Asia/Jakarta'; country = 'ID'; zh = '雅加达'; en = 'Jakarta' }
    @{ zone = 'Asia/Manila'; country = 'PH'; zh = '马尼拉'; en = 'Manila' }
    @{ zone = 'Asia/Kolkata'; country = 'IN'; zh = '加尔各答'; en = 'Kolkata' }
    @{ zone = 'Asia/Karachi'; country = 'PK'; zh = '卡拉奇'; en = 'Karachi' }
    @{ zone = 'Asia/Dhaka'; country = 'BD'; zh = '达卡'; en = 'Dhaka' }
    @{ zone = 'Asia/Colombo'; country = 'LK'; zh = '科伦坡'; en = 'Colombo' }
    @{ zone = 'Asia/Kathmandu'; country = 'NP'; zh = '加德满都'; en = 'Kathmandu' }
    @{ zone = 'Asia/Dubai'; country = 'AE'; zh = '迪拜'; en = 'Dubai' }
    @{ zone = 'Asia/Riyadh'; country = 'SA'; zh = '利雅得'; en = 'Riyadh' }
    @{ zone = 'Asia/Qatar'; country = 'QA'; zh = '多哈'; en = 'Doha' }
    @{ zone = 'Asia/Kuwait'; country = 'KW'; zh = '科威特城'; en = 'Kuwait City' }
    @{ zone = 'Asia/Bahrain'; country = 'BH'; zh = '麦纳麦'; en = 'Manama' }
    @{ zone = 'Asia/Muscat'; country = 'OM'; zh = '马斯喀特'; en = 'Muscat' }
    @{ zone = 'Asia/Jerusalem'; country = 'IL'; zh = '耶路撒冷'; en = 'Jerusalem' }
    @{ zone = 'Asia/Istanbul'; country = 'TR'; zh = '伊斯坦布尔'; en = 'Istanbul' }
    @{ zone = 'Africa/Cairo'; country = 'EG'; zh = '开罗'; en = 'Cairo' }
    @{ zone = 'Africa/Algiers'; country = 'DZ'; zh = '阿尔及尔'; en = 'Algiers' }
    @{ zone = 'Asia/Baghdad'; country = 'IQ'; zh = '巴格达'; en = 'Baghdad' }
    @{ zone = 'Asia/Amman'; country = 'JO'; zh = '安曼'; en = 'Amman' }
    @{ zone = 'Asia/Beirut'; country = 'LB'; zh = '贝鲁特'; en = 'Beirut' }
    @{ zone = 'Africa/Johannesburg'; country = 'ZA'; zh = '约翰内斯堡'; en = 'Johannesburg' }
    @{ zone = 'Africa/Lagos'; country = 'NG'; zh = '拉各斯'; en = 'Lagos' }
    @{ zone = 'Africa/Nairobi'; country = 'KE'; zh = '内罗毕'; en = 'Nairobi' }
    @{ zone = 'Africa/Casablanca'; country = 'MA'; zh = '卡萨布兰卡'; en = 'Casablanca' }
    @{ zone = 'Asia/Almaty'; country = 'KZ'; zh = '阿拉木图'; en = 'Almaty' }
    @{ zone = 'Asia/Tashkent'; country = 'UZ'; zh = '塔什干'; en = 'Tashkent' }
    @{ zone = 'Asia/Baku'; country = 'AZ'; zh = '巴库'; en = 'Baku' }
    @{ zone = 'Asia/Tbilisi'; country = 'GE'; zh = '第比利斯'; en = 'Tbilisi' }
    @{ zone = 'Asia/Yerevan'; country = 'AM'; zh = '埃里温'; en = 'Yerevan' }
    @{ zone = 'Asia/Tehran'; country = 'IR'; zh = '德黑兰'; en = 'Tehran' }
    @{ zone = 'Asia/Kabul'; country = 'AF'; zh = '喀布尔'; en = 'Kabul' }
    @{ zone = 'Asia/Ulaanbaatar'; country = 'MN'; zh = '乌兰巴托'; en = 'Ulaanbaatar' }
    @{ zone = 'Asia/Yangon'; country = 'MM'; zh = '仰光'; en = 'Yangon' }
    @{ zone = 'Asia/Phnom_Penh'; country = 'KH'; zh = '金边'; en = 'Phnom Penh' }
    @{ zone = 'Asia/Vientiane'; country = 'LA'; zh = '万象'; en = 'Vientiane' }
    @{ zone = 'Asia/Brunei'; country = 'BN'; zh = '斯里巴加湾市'; en = 'Bandar Seri Begawan' }
    @{ zone = 'Europe/London'; country = 'GB'; zh = '伦敦'; en = 'London' }
    @{ zone = 'Europe/Dublin'; country = 'IE'; zh = '都柏林'; en = 'Dublin' }
    @{ zone = 'Europe/Paris'; country = 'FR'; zh = '巴黎'; en = 'Paris' }
    @{ zone = 'Europe/Berlin'; country = 'DE'; zh = '柏林'; en = 'Berlin' }
    @{ zone = 'Europe/Amsterdam'; country = 'NL'; zh = '阿姆斯特丹'; en = 'Amsterdam' }
    @{ zone = 'Europe/Brussels'; country = 'BE'; zh = '布鲁塞尔'; en = 'Brussels' }
    @{ zone = 'Europe/Madrid'; country = 'ES'; zh = '马德里'; en = 'Madrid' }
    @{ zone = 'Europe/Lisbon'; country = 'PT'; zh = '里斯本'; en = 'Lisbon' }
    @{ zone = 'Europe/Rome'; country = 'IT'; zh = '罗马'; en = 'Rome' }
    @{ zone = 'Europe/Zurich'; country = 'CH'; zh = '苏黎世'; en = 'Zurich' }
    @{ zone = 'Europe/Vienna'; country = 'AT'; zh = '维也纳'; en = 'Vienna' }
    @{ zone = 'Europe/Stockholm'; country = 'SE'; zh = '斯德哥尔摩'; en = 'Stockholm' }
    @{ zone = 'Europe/Oslo'; country = 'NO'; zh = '奥斯陆'; en = 'Oslo' }
    @{ zone = 'Europe/Copenhagen'; country = 'DK'; zh = '哥本哈根'; en = 'Copenhagen' }
    @{ zone = 'Europe/Helsinki'; country = 'FI'; zh = '赫尔辛基'; en = 'Helsinki' }
    @{ zone = 'Europe/Warsaw'; country = 'PL'; zh = '华沙'; en = 'Warsaw' }
    @{ zone = 'Europe/Prague'; country = 'CZ'; zh = '布拉格'; en = 'Prague' }
    @{ zone = 'Europe/Budapest'; country = 'HU'; zh = '布达佩斯'; en = 'Budapest' }
    @{ zone = 'Europe/Bucharest'; country = 'RO'; zh = '布加勒斯特'; en = 'Bucharest' }
    @{ zone = 'Europe/Athens'; country = 'GR'; zh = '雅典'; en = 'Athens' }
    @{ zone = 'Europe/Kiev'; country = 'UA'; zh = '基辅'; en = 'Kyiv' }
    @{ zone = 'Europe/Moscow'; country = 'RU'; zh = '莫斯科'; en = 'Moscow' }
    @{ zone = 'Atlantic/Reykjavik'; country = 'IS'; zh = '雷克雅未克'; en = 'Reykjavik' }
    @{ zone = 'Europe/Luxembourg'; country = 'LU'; zh = '卢森堡'; en = 'Luxembourg' }
    @{ zone = 'Europe/Malta'; country = 'MT'; zh = '瓦莱塔'; en = 'Valletta' }
    @{ zone = 'Asia/Nicosia'; country = 'CY'; zh = '尼科西亚'; en = 'Nicosia' }
    @{ zone = 'Europe/Sofia'; country = 'BG'; zh = '索菲亚'; en = 'Sofia' }
    @{ zone = 'Europe/Zagreb'; country = 'HR'; zh = '萨格勒布'; en = 'Zagreb' }
    @{ zone = 'Europe/Belgrade'; country = 'RS'; zh = '贝尔格莱德'; en = 'Belgrade' }
    @{ zone = 'Europe/Ljubljana'; country = 'SI'; zh = '卢布尔雅那'; en = 'Ljubljana' }
    @{ zone = 'Europe/Bratislava'; country = 'SK'; zh = '布拉迪斯拉发'; en = 'Bratislava' }
    @{ zone = 'Europe/Vilnius'; country = 'LT'; zh = '维尔纽斯'; en = 'Vilnius' }
    @{ zone = 'Europe/Riga'; country = 'LV'; zh = '里加'; en = 'Riga' }
    @{ zone = 'Europe/Tallinn'; country = 'EE'; zh = '塔林'; en = 'Tallinn' }
    @{ zone = 'Europe/Minsk'; country = 'BY'; zh = '明斯克'; en = 'Minsk' }
    @{ zone = 'Europe/Chisinau'; country = 'MD'; zh = '基希讷乌'; en = 'Chisinau' }
    @{ zone = 'America/New_York'; country = 'US'; zh = '纽约'; en = 'New York' }
    @{ zone = 'America/Chicago'; country = 'US'; zh = '芝加哥'; en = 'Chicago' }
    @{ zone = 'America/Denver'; country = 'US'; zh = '丹佛'; en = 'Denver' }
    @{ zone = 'America/Los_Angeles'; country = 'US'; zh = '旧金山'; en = 'San Francisco' }
    @{ zone = 'America/Phoenix'; country = 'US'; zh = '凤凰城'; en = 'Phoenix' }
    @{ zone = 'America/Anchorage'; country = 'US'; zh = '安克雷奇'; en = 'Anchorage' }
    @{ zone = 'Pacific/Honolulu'; country = 'US'; zh = '檀香山'; en = 'Honolulu' }
    @{ zone = 'America/Toronto'; country = 'CA'; zh = '多伦多'; en = 'Toronto' }
    @{ zone = 'America/Vancouver'; country = 'CA'; zh = '温哥华'; en = 'Vancouver' }
    @{ zone = 'America/Edmonton'; country = 'CA'; zh = '卡尔加里'; en = 'Calgary' }
    @{ zone = 'America/Halifax'; country = 'CA'; zh = '哈利法克斯'; en = 'Halifax' }
    @{ zone = 'America/Winnipeg'; country = 'CA'; zh = '温尼伯'; en = 'Winnipeg' }
    @{ zone = 'America/Mexico_City'; country = 'MX'; zh = '墨西哥城'; en = 'Mexico City' }
    @{ zone = 'America/Tijuana'; country = 'MX'; zh = '蒂华纳'; en = 'Tijuana' }
    @{ zone = 'America/Sao_Paulo'; country = 'BR'; zh = '圣保罗'; en = 'Sao Paulo' }
    @{ zone = 'America/Manaus'; country = 'BR'; zh = '马瑙斯'; en = 'Manaus' }
    @{ zone = 'America/Argentina/Buenos_Aires'; country = 'AR'; zh = '布宜诺斯艾利斯'; en = 'Buenos Aires' }
    @{ zone = 'America/Santiago'; country = 'CL'; zh = '圣地亚哥'; en = 'Santiago' }
    @{ zone = 'America/Bogota'; country = 'CO'; zh = '波哥大'; en = 'Bogota' }
    @{ zone = 'America/Lima'; country = 'PE'; zh = '利马'; en = 'Lima' }
    @{ zone = 'America/Caracas'; country = 'VE'; zh = '加拉加斯'; en = 'Caracas' }
    @{ zone = 'America/Montevideo'; country = 'UY'; zh = '蒙得维的亚'; en = 'Montevideo' }
    @{ zone = 'America/Guayaquil'; country = 'EC'; zh = '瓜亚基尔'; en = 'Guayaquil' }
    @{ zone = 'America/La_Paz'; country = 'BO'; zh = '拉巴斯'; en = 'La Paz' }
    @{ zone = 'America/Asuncion'; country = 'PY'; zh = '亚松森'; en = 'Asuncion' }
    @{ zone = 'America/Costa_Rica'; country = 'CR'; zh = '圣何塞'; en = 'San Jose' }
    @{ zone = 'America/Panama'; country = 'PA'; zh = '巴拿马城'; en = 'Panama City' }
    @{ zone = 'America/Santo_Domingo'; country = 'DO'; zh = '圣多明各'; en = 'Santo Domingo' }
    @{ zone = 'America/Guatemala'; country = 'GT'; zh = '危地马拉城'; en = 'Guatemala City' }
    @{ zone = 'America/Havana'; country = 'CU'; zh = '哈瓦那'; en = 'Havana' }
    @{ zone = 'America/Jamaica'; country = 'JM'; zh = '金斯敦'; en = 'Kingston' }
    @{ zone = 'America/Port_of_Spain'; country = 'TT'; zh = '西班牙港'; en = 'Port of Spain' }
    @{ zone = 'Australia/Sydney'; country = 'AU'; zh = '悉尼'; en = 'Sydney' }
    @{ zone = 'Australia/Melbourne'; country = 'AU'; zh = '墨尔本'; en = 'Melbourne' }
    @{ zone = 'Australia/Brisbane'; country = 'AU'; zh = '布里斯班'; en = 'Brisbane' }
    @{ zone = 'Australia/Perth'; country = 'AU'; zh = '珀斯'; en = 'Perth' }
    @{ zone = 'Australia/Adelaide'; country = 'AU'; zh = '阿德莱德'; en = 'Adelaide' }
    @{ zone = 'Australia/Darwin'; country = 'AU'; zh = '达尔文'; en = 'Darwin' }
    @{ zone = 'Australia/Hobart'; country = 'AU'; zh = '霍巴特'; en = 'Hobart' }
    @{ zone = 'Pacific/Auckland'; country = 'NZ'; zh = '奥克兰'; en = 'Auckland' }
    @{ zone = 'Pacific/Fiji'; country = 'FJ'; zh = '苏瓦'; en = 'Suva' }
    @{ zone = 'Pacific/Port_Moresby'; country = 'PG'; zh = '莫尔兹比港'; en = 'Port Moresby' }
)

$cities = [System.Collections.Generic.List[object]]::new()
$seenZones = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($city in $cityCatalog) {
    if (-not $countryCodes.Contains($city.country)) {
        throw "City $($city.en) references unknown country $($city.country)."
    }

    if (-not $knownZones.Contains($city.zone)) {
        throw "City $($city.en) references unknown zone $($city.zone)."
    }

    if (-not $seenZones.Add($city.zone)) {
        throw "Zone $($city.zone) is used by more than one city."
    }

    $cities.Add([ordered]@{
            id = $city.zone
            country = $city.country
            name = [ordered]@{ zh = $city.zh; en = $city.en }
        })
}

$cities = $cities | Sort-Object -Property { $_.country }, { $_.name.en }

# Every emitted zone must be resolvable by the system the application runs on.
foreach ($country in $countries) {
    $resolved = $null
    if (-not [TimeZoneInfo]::TryFindSystemTimeZoneById($country.representative.zone, [ref]$resolved)) {
        throw "Zone $($country.representative.zone) of $($country.iso2) is not resolvable by this system."
    }
}

foreach ($city in $cities) {
    $resolved = $null
    if (-not [TimeZoneInfo]::TryFindSystemTimeZoneById($city.id, [ref]$resolved)) {
        throw "Zone $($city.id) of $($city.name.en) is not resolvable by this system."
    }
}

# Office hours ---------------------------------------------------------------------

$workWeek = @(1, 2, 3, 4, 5)
$sunThu = @(7, 1, 2, 3, 4)
$hoursSource = 'https://en.wikipedia.org/wiki/Workweek_and_weekend'
$updatedAt = '2026-10-28'

$officeHours = [ordered]@{
    global = [ordered]@{
        start = '09:00'; end = '18:00'; days = $workWeek; source = $hoursSource; updatedAt = $updatedAt
    }
    overrides = @(
        [ordered]@{ country = 'SA'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'QA'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'KW'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'BH'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'OM'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'IL'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'EG'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'JO'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'IQ'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
        [ordered]@{ country = 'DZ'; start = '09:00'; end = '18:00'; days = $sunThu; source = $hoursSource; updatedAt = $updatedAt }
    )
}

$source = [ordered]@{
    cldrVersion = $cldrVersion._cldrVersion
    unicodeVersion = $cldrVersion._unicodeVersion
    tzdbVersion = $tzdbVersion
    libphonenumberCommit = $phoneCommit
    sources = [ordered]@{
        names = 'https://github.com/unicode-org/cldr-json'
        zones = 'https://github.com/eggert/tz'
        dialCodes = 'https://github.com/google/libphonenumber'
    }
}

function ConvertTo-ReadableJson {
    param($Value)

    $json = $Value | ConvertTo-Json -Depth 10
    return [regex]::Replace(
        $json,
        '\\u([0-9a-fA-F]{4})',
        { param($match) [string][char][Convert]::ToInt32($match.Groups[1].Value, 16) })
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

[System.IO.File]::WriteAllText(
    (Join-Path $OutputDirectory 'countries.json'),
    (ConvertTo-ReadableJson -Value ([ordered]@{ source = $source; countries = $countries })),
    $utf8)
[System.IO.File]::WriteAllText(
    (Join-Path $OutputDirectory 'cities.json'),
    (ConvertTo-ReadableJson -Value ([ordered]@{ source = $source; cities = $cities })),
    $utf8)
[System.IO.File]::WriteAllText(
    (Join-Path $OutputDirectory 'office-hours.json'),
    (ConvertTo-ReadableJson -Value ([ordered]@{ source = $source; global = $officeHours.global; overrides = $officeHours.overrides })),
    $utf8)

Write-Host "Wrote $($countries.Count) countries, $($cities.Count) cities and $($officeHours.overrides.Count) office-hours overrides to $OutputDirectory."
