<#
.SYNOPSIS
Validates a JiuMi character pack (manifest.json + PNG frames) for format integrity, L1 zero-drift,
L2/L3 continuity, and exports visual review sheets.

.DESCRIPTION
Runs the Phase 1 automated QA gate for My JiuMi character packs:
1. Manifest schema (schemaVersion: 1), required clips, path safety, optional SHA-256 checksums.
2. PNG format check: 400x400 (matching manifest canvas), 8-bit RGBA (color type 6, bit depth 8),
   missing/orphan PNG detection, and extraneous PNG text/metadata chunk detection.
3. L1 zero-drift gate (lying-idle, blink, sleep, react, drag, plus cross-direction lower-body
   zero-drift across interactions.sit-watch-left/center/right when declared): exact pixel diff = 0
   ratio against frame 0 (or sit-watch-center lower body) over the union opaque mask.
4. L2/L3 continuity gate: adjacent-frame foot y_max fluctuation, alpha-weighted centroid jump
   (> MaxCentroidJumpPx), and bounding-box scale anomaly (> MaxBBoxScaleChangePct).
5. Review artifact export:
   - contact-sheet.png (all frames with explicit anchor crosshair)
   - onion-skin/<clip>.png (translucent frame overlay per multi-frame clip and interactions-sit-watch)
   - preview-light-dark-200dip.png (200 DIP previews on light and dark room backgrounds)
   - report.json (machine-readable metrics and gate results)

.PARAMETER PackDir
Path to the character pack directory containing manifest.json.

.PARAMETER OutputDir
Directory where review images and report.json are written. Defaults to artifacts/pet-qa/<pack-id>.

.PARAMETER MaxCentroidJumpPx
Maximum allowed adjacent-frame centroid jump in canvas pixels (default: 6.0).

.PARAMETER MaxBBoxScaleChangePct
Maximum allowed adjacent-frame bounding-box scale change in percent (default: 5.0).

.PARAMETER MinL1ExactMatchPct
Minimum required L1 static-body exact-match (diff = 0) percentage (default: 80.0).

.PARAMETER AllowOrphans
Report unreferenced PNG files as warnings instead of hard failures.

.PARAMETER L1Only
Enforce format and L1 zero-drift gates (including sit-watch lower-body zero-drift) while skipping
L2/L3 continuity hard failures during staged L1 cutovers.

.PARAMETER ReportOnly
Exit 0 even when quality gates fail (useful for baselining legacy v1 assets).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackDir,

    [string]$OutputDir,

    [int]$ExpectedCanvasSize = 400,

    [double]$MaxCentroidJumpPx = 6.0,

    [double]$MaxBBoxScaleChangePct = 5.0,

    [double]$MinL1ExactMatchPct = 80.0,

    [double]$MinSitWatchLowerBodyMatchPct = 99.0,

    [double]$SitWatchLowerBodyRatio = 0.495,

    [switch]$AllowOrphans,

    [switch]$L1Only,

    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'

$resolvedPackDir = (Resolve-Path -LiteralPath $PackDir).Path
$manifestPath = Join-Path $resolvedPackDir 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "manifest.json not found in '$resolvedPackDir'."
}

Add-Type -AssemblyName System.Drawing
$drawingAssemblies = @(
    'System.Drawing.Common'
    'System.Drawing.Primitives'
    'System.Collections'
    'System.Runtime'
)
$extraGdi = [System.AppDomain]::CurrentDomain.GetAssemblies() |
    Where-Object { $_.GetName().Name -like 'System.Private.Windows.*' -and -not [string]::IsNullOrEmpty($_.Location) } |
    ForEach-Object { $_.Location }
$drawingAssemblies = @($drawingAssemblies + $extraGdi) | Select-Object -Unique

if (-not ('JiuMiPetPackQaTool' -as [type])) {
    Add-Type -ReferencedAssemblies $drawingAssemblies -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public sealed class PetPngInfo
{
    public bool IsValidPng { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int BitDepth { get; set; }
    public int ColorType { get; set; }
    public List<string> ExtraChunks { get; set; } = new List<string>();
    public string Error { get; set; } = string.Empty;
}

public sealed class PetFrameMetrics
{
    public string Clip { get; set; } = string.Empty;
    public int Index { get; set; }
    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public double AnchorX { get; set; }
    public double AnchorY { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int OpaquePixels { get; set; }
    public int MinX { get; set; }
    public int MinY { get; set; }
    public int MaxX { get; set; }
    public int MaxY { get; set; }
    public double CentroidX { get; set; }
    public double CentroidY { get; set; }
    public double BBoxDiagonal { get; set; }
}

public static class JiuMiPetPackQaTool
{
    private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
    private static readonly HashSet<string> DisallowedChunks = new HashSet<string>(StringComparer.Ordinal)
    {
        "tEXt", "zTXt", "iTXt", "eXIf"
    };

    public static PetPngInfo InspectPng(string path)
    {
        var info = new PetPngInfo();
        try
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < 33)
            {
                info.Error = "File too small to be a valid PNG.";
                return info;
            }

            for (int i = 0; i < 8; i++)
            {
                if (data[i] != PngSignature[i])
                {
                    info.Error = "Invalid PNG signature.";
                    return info;
                }
            }

            int pos = 8;
            bool sawIhdr = false;
            while (pos + 12 <= data.Length)
            {
                uint length = ReadUInt32BE(data, pos);
                string type = Encoding.ASCII.GetString(data, pos + 4, 4);
                int dataStart = pos + 8;
                if (dataStart + length + 4 > data.Length)
                {
                    info.Error = "Truncated PNG chunk: " + type;
                    return info;
                }

                if (type == "IHDR" && length >= 13)
                {
                    info.Width = (int)ReadUInt32BE(data, dataStart);
                    info.Height = (int)ReadUInt32BE(data, dataStart + 4);
                    info.BitDepth = data[dataStart + 8];
                    info.ColorType = data[dataStart + 9];
                    sawIhdr = true;
                }
                else if (DisallowedChunks.Contains(type))
                {
                    info.ExtraChunks.Add(type);
                }

                pos = dataStart + (int)length + 4;
                if (type == "IEND")
                {
                    break;
                }
            }

            info.IsValidPng = sawIhdr;
            if (!sawIhdr)
            {
                info.Error = "Missing IHDR chunk.";
            }
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }

        return info;
    }

    public static PetFrameMetrics MeasureFrame(string clip, int index, string relativePath, string fullPath, double anchorX, double anchorY)
    {
        var m = new PetFrameMetrics
        {
            Clip = clip,
            Index = index,
            RelativePath = relativePath,
            FullPath = fullPath,
            AnchorX = anchorX,
            AnchorY = anchorY
        };

        using (var bmp = new Bitmap(fullPath))
        {
            m.Width = bmp.Width;
            m.Height = bmp.Height;
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bd = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = bd.Stride;
                byte[] buffer = new byte[stride * bmp.Height];
                Marshal.Copy(bd.Scan0, buffer, 0, buffer.Length);

                int minX = bmp.Width;
                int minY = bmp.Height;
                int maxX = -1;
                int maxY = -1;
                double sumW = 0;
                double sumX = 0;
                double sumY = 0;
                int opaque = 0;

                for (int y = 0; y < bmp.Height; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        byte a = buffer[row + (x * 4) + 3];
                        if (a > 0)
                        {
                            opaque++;
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            sumW += a;
                            sumX += x * (double)a;
                            sumY += y * (double)a;
                        }
                    }
                }

                m.OpaquePixels = opaque;
                if (opaque > 0)
                {
                    m.MinX = minX;
                    m.MinY = minY;
                    m.MaxX = maxX;
                    m.MaxY = maxY;
                    m.CentroidX = sumX / sumW;
                    m.CentroidY = sumY / sumW;
                    double bw = (maxX - minX) + 1;
                    double bh = (maxY - minY) + 1;
                    m.BBoxDiagonal = Math.Sqrt((bw * bw) + (bh * bh));
                }
            }
            finally
            {
                bmp.UnlockBits(bd);
            }
        }

        return m;
    }

    public static double ComputeExactMatchPercent(string basePath, string comparePath)
    {
        return ComputeExactMatchPercentBelowY(basePath, comparePath, 0);
    }

    public static double ComputeExactMatchPercentBelowY(string basePath, string comparePath, int minY)
    {
        using (var bmpA = new Bitmap(basePath))
        using (var bmpB = new Bitmap(comparePath))
        {
            if (bmpA.Width != bmpB.Width || bmpA.Height != bmpB.Height)
            {
                return 0.0;
            }

            var rect = new Rectangle(0, 0, bmpA.Width, bmpA.Height);
            BitmapData bdA = bmpA.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData bdB = bmpB.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] bufA = new byte[bdA.Stride * bmpA.Height];
                byte[] bufB = new byte[bdB.Stride * bmpB.Height];
                Marshal.Copy(bdA.Scan0, bufA, 0, bufA.Length);
                Marshal.Copy(bdB.Scan0, bufB, 0, bufB.Length);

                int unionCount = 0;
                int exactCount = 0;
                int startY = Math.Clamp(minY, 0, bmpA.Height);
                for (int y = startY; y < bmpA.Height; y++)
                {
                    int rowA = y * bdA.Stride;
                    int rowB = y * bdB.Stride;
                    for (int x = 0; x < bmpA.Width; x++)
                    {
                        int offA = rowA + (x * 4);
                        int offB = rowB + (x * 4);
                        byte aA = bufA[offA + 3];
                        byte aB = bufB[offB + 3];
                        if (aA == 0 && aB == 0)
                        {
                            continue;
                        }

                        unionCount++;
                        if (bufA[offA] == bufB[offB] &&
                            bufA[offA + 1] == bufB[offB + 1] &&
                            bufA[offA + 2] == bufB[offB + 2] &&
                            aA == aB)
                        {
                            exactCount++;
                        }
                    }
                }

                return unionCount == 0 ? 100.0 : (exactCount * 100.0) / unionCount;
            }
            finally
            {
                bmpA.UnlockBits(bdA);
                bmpB.UnlockBits(bdB);
            }
        }
    }

    public static void RenderContactSheet(PetFrameMetrics[] frames, string outputPath)
    {
        if (frames.Length == 0)
        {
            return;
        }

        const int cols = 6;
        const int cellSize = 180;
        const int labelHeight = 26;
        const int pad = 8;
        int rows = (int)Math.Ceiling(frames.Length / (double)cols);
        int width = pad + cols * (cellSize + pad);
        int height = pad + rows * (cellSize + labelHeight + pad);

        using (var sheet = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(sheet))
        using (var font = new Font("Segoe UI", 9f, FontStyle.Regular))
        using (var labelBg = new SolidBrush(Color.FromArgb(255, 36, 36, 40)))
        using (var textBrush = new SolidBrush(Color.White))
        using (var borderPen = new Pen(Color.FromArgb(255, 110, 110, 120), 1f))
        using (var crossPen = new Pen(Color.FromArgb(220, 230, 45, 70), 1.5f))
        {
            g.Clear(Color.FromArgb(255, 24, 24, 28));
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            for (int i = 0; i < frames.Length; i++)
            {
                PetFrameMetrics f = frames[i];
                int col = i % cols;
                int row = i / cols;
                int x = pad + col * (cellSize + pad);
                int y = pad + row * (cellSize + labelHeight + pad);

                DrawCheckerboard(g, x, y, cellSize, cellSize, 12);
                using (var frameBmp = new Bitmap(f.FullPath))
                {
                    g.DrawImage(frameBmp, new Rectangle(x, y, cellSize, cellSize));
                }

                float ax = x + (float)(f.AnchorX / Math.Max(1, f.Width) * cellSize);
                float ay = y + (float)(f.AnchorY / Math.Max(1, f.Height) * cellSize);
                g.DrawLine(crossPen, ax - 10, ay, ax + 10, ay);
                g.DrawLine(crossPen, ax, ay - 10, ax, ay + 10);
                g.DrawRectangle(borderPen, x, y, cellSize, cellSize);

                g.FillRectangle(labelBg, x, y + cellSize, cellSize, labelHeight);
                string label = string.Format("{0}[{1}] ({2:0},{3:0})", f.Clip, f.Index, f.AnchorX, f.AnchorY);
                g.DrawString(label, font, textBrush, x + 4, y + cellSize + 5);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            sheet.Save(outputPath, ImageFormat.Png);
        }
    }

    public static void RenderOnionSkin(PetFrameMetrics[] clipFrames, string outputPath)
    {
        if (clipFrames.Length == 0)
        {
            return;
        }

        int width = Math.Max(1, clipFrames[0].Width);
        int height = Math.Max(1, clipFrames[0].Height);
        using (var canvas = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(canvas))
        using (var crossPen = new Pen(Color.FromArgb(230, 230, 45, 70), 2f))
        {
            DrawCheckerboard(g, 0, 0, width, height, 16);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            for (int i = 0; i < clipFrames.Length; i++)
            {
                float alpha = i == 0 ? 0.65f : Math.Max(0.15f, 0.55f / Math.Max(1, clipFrames.Length - 1));
                var cm = new ColorMatrix { Matrix33 = alpha };
                using (var attrs = new ImageAttributes())
                using (var bmp = new Bitmap(clipFrames[i].FullPath))
                {
                    attrs.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    g.DrawImage(
                        bmp,
                        new Rectangle(0, 0, width, height),
                        0, 0, bmp.Width, bmp.Height,
                        GraphicsUnit.Pixel,
                        attrs);
                }
            }

            float ax = (float)clipFrames[0].AnchorX;
            float ay = (float)clipFrames[0].AnchorY;
            g.DrawLine(crossPen, 0, ay, width, ay);
            g.DrawLine(crossPen, ax, 0, ax, height);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            canvas.Save(outputPath, ImageFormat.Png);
        }
    }

    public static void RenderLightDarkPreview(PetFrameMetrics[] sampleFrames, string outputPath)
    {
        if (sampleFrames.Length == 0)
        {
            return;
        }

        const int slot = 200;
        const int floorStrip = 36;
        const int pad = 16;
        const int header = 28;
        int cols = sampleFrames.Length;
        int panelWidth = pad + cols * (slot + pad);
        int panelHeight = header + slot + floorStrip + pad;
        int totalHeight = panelHeight * 2;

        using (var canvas = new Bitmap(panelWidth, totalHeight, PixelFormat.Format32bppArgb))
        using (var g = Graphics.FromImage(canvas))
        using (var font = new Font("Segoe UI", 10f, FontStyle.Bold))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawPreviewBand(g, sampleFrames, 0, panelWidth, panelHeight, header, slot, floorStrip, pad,
                Color.FromArgb(255, 245, 245, 247),
                Color.FromArgb(255, 232, 232, 236),
                Color.FromArgb(255, 200, 200, 208),
                Color.FromArgb(255, 32, 32, 36),
                "Light 200 DIP Preview",
                font);

            DrawPreviewBand(g, sampleFrames, panelHeight, panelWidth, panelHeight, header, slot, floorStrip, pad,
                Color.FromArgb(255, 32, 32, 36),
                Color.FromArgb(255, 42, 42, 48),
                Color.FromArgb(255, 72, 72, 82),
                Color.FromArgb(255, 238, 238, 242),
                "Dark 200 DIP Preview",
                font);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            canvas.Save(outputPath, ImageFormat.Png);
        }
    }

    private static void DrawPreviewBand(
        Graphics g,
        PetFrameMetrics[] frames,
        int top,
        int panelWidth,
        int panelHeight,
        int header,
        int slot,
        int floorStrip,
        int pad,
        Color wallColor,
        Color floorColor,
        Color dividerColor,
        Color textColor,
        string title,
        Font font)
    {
        int floorY = top + header + slot;
        using (var wallBrush = new SolidBrush(wallColor))
        using (var floorBrush = new SolidBrush(floorColor))
        using (var dividerPen = new Pen(dividerColor, 1f))
        using (var textBrush = new SolidBrush(textColor))
        {
            g.FillRectangle(wallBrush, 0, top, panelWidth, header + slot);
            g.FillRectangle(floorBrush, 0, floorY, panelWidth, floorStrip + pad);
            g.DrawLine(dividerPen, 0, floorY, panelWidth, floorY);
            g.DrawString(title, font, textBrush, pad, top + 6);

            for (int i = 0; i < frames.Length; i++)
            {
                PetFrameMetrics f = frames[i];
                int slotX = pad + i * (slot + pad);
                double normX = f.Width > 0 ? Math.Clamp(f.AnchorX / f.Width, 0.0, 1.0) : 0.5;
                double normY = f.Height > 0
                    ? (f.AnchorY >= f.Height - 1 ? 1.0 : Math.Clamp(f.AnchorY / f.Height, 0.0, 1.0))
                    : 1.0;
                float dx = (float)((0.5 - normX) * slot);
                float dy = (float)((1.0 - normY) * slot);

                using (var bmp = new Bitmap(f.FullPath))
                {
                    g.DrawImage(bmp, new RectangleF(slotX + dx, top + header + dy, slot, slot));
                }

                g.DrawString(f.Clip, font, textBrush, slotX + 4, floorY + 8);
            }
        }
    }

    private static void DrawCheckerboard(Graphics g, int x, int y, int w, int h, int tile)
    {
        using (var light = new SolidBrush(Color.FromArgb(255, 224, 224, 228)))
        using (var dark = new SolidBrush(Color.FromArgb(255, 196, 196, 202)))
        {
            for (int ty = 0; ty < h; ty += tile)
            {
                for (int tx = 0; tx < w; tx += tile)
                {
                    Brush b = (((tx / tile) + (ty / tile)) % 2 == 0) ? light : dark;
                    g.FillRectangle(b, x + tx, y + ty, Math.Min(tile, w - tx), Math.Min(tile, h - ty));
                }
            }
        }
    }

    private static uint ReadUInt32BE(byte[] data, int offset) =>
        ((uint)data[offset] << 24) |
        ((uint)data[offset + 1] << 16) |
        ((uint)data[offset + 2] << 8) |
        data[offset + 3];
}
'@
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable

$errors = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

if ($manifest.schemaVersion -ne 1) {
    $errors.Add("Unsupported schemaVersion '$($manifest.schemaVersion)'; expected 1.")
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.id)) {
    $errors.Add("Manifest 'id' is required.")
}
if ([string]::IsNullOrWhiteSpace([string]$manifest.name)) {
    $errors.Add("Manifest 'name' is required.")
}

$canvasWidth = [int]$manifest.canvas.width
$canvasHeight = [int]$manifest.canvas.height
if ($canvasWidth -le 0 -or $canvasHeight -le 0) {
    $errors.Add("Manifest 'canvas' must have positive width and height.")
}
elseif ($ExpectedCanvasSize -gt 0 -and ($canvasWidth -ne $ExpectedCanvasSize -or $canvasHeight -ne $ExpectedCanvasSize)) {
    $errors.Add("Manifest 'canvas' is ${canvasWidth}x${canvasHeight}; expected ${ExpectedCanvasSize}x${ExpectedCanvasSize}.")
}

function Resolve-SafePackPath {
    param([string]$RelativePath)

    if ([string]::IsNullOrWhiteSpace($RelativePath)) {
        return $null
    }

    $normalized = $RelativePath.Trim().Replace('\', '/')
    if ($normalized.StartsWith('/') -or $normalized.EndsWith('/') -or $normalized.Contains(':')) {
        return $null
    }

    foreach ($segment in $normalized.Split('/')) {
        if ([string]::IsNullOrEmpty($segment) -or $segment -eq '.' -or $segment -eq '..') {
            return $null
        }
    }

    $combined = [System.IO.Path]::GetFullPath((Join-Path $resolvedPackDir ($normalized.Replace('/', [System.IO.Path]::DirectorySeparatorChar))))
    $packPrefix = $resolvedPackDir.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $combined.StartsWith($packPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $null
    }

    return $combined
}

$defaultAnchorX = [double]$manifest.defaultAnchor.x
$defaultAnchorY = [double]$manifest.defaultAnchor.y

$requiredClips = @('lying-idle', 'blink', 'react', 'drag', 'sleep-in', 'sleep', 'wake', 'stretch', 'walk')
$l1Clips = @('lying-idle', 'blink', 'sleep', 'react', 'drag')

foreach ($req in $requiredClips) {
    if (-not $manifest.clips.ContainsKey($req)) {
        $errors.Add("Missing required core clip '$req'.")
    }
}

$packId = if ([string]::IsNullOrWhiteSpace([string]$manifest.id)) { 'unknown-pack' } else { [string]$manifest.id }
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $OutputDir = Join-Path $repoRoot (Join-Path 'artifacts' (Join-Path 'pet-qa' $packId))
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$referencedPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
$allFrameMetrics = [System.Collections.Generic.List[PetFrameMetrics]]::new()
$clipReports = [ordered]@{}

function Process-ClipCollection {
    param(
        [hashtable]$Collection,
        [string]$Prefix
    )

    if ($null -eq $Collection) {
        return
    }

    foreach ($clipKey in ($Collection.Keys | Sort-Object)) {
        $clipObj = $Collection[$clipKey]
        $displayKey = if ($Prefix) { "$Prefix.$clipKey" } else { $clipKey }
        $clipAnchorX = if ($clipObj.ContainsKey('anchor') -and $null -ne $clipObj.anchor) { [double]$clipObj.anchor.x } else { $defaultAnchorX }
        $clipAnchorY = if ($clipObj.ContainsKey('anchor') -and $null -ne $clipObj.anchor) { [double]$clipObj.anchor.y } else { $defaultAnchorY }

        $frames = @($clipObj.frames)
        if ($frames.Count -eq 0) {
            $errors.Add("Clip '$displayKey' has no frames.")
            continue
        }

        $clipMetrics = [System.Collections.Generic.List[PetFrameMetrics]]::new()
        $frameSummaries = [System.Collections.Generic.List[hashtable]]::new()

        for ($i = 0; $i -lt $frames.Count; $i++) {
            $f = $frames[$i]
            $relPath = ([string]$f.path).Replace('\', '/')
            [void]$referencedPaths.Add($relPath)

            if ([int]$f.durationMs -le 0) {
                $errors.Add("Clip '$displayKey' frame $i has non-positive durationMs '$($f.durationMs)'.")
            }

            $fullPath = Resolve-SafePackPath -RelativePath $relPath
            if ($null -eq $fullPath) {
                $errors.Add("Unsafe or invalid relative path '$relPath' referenced by '$displayKey'[$i].")
                continue
            }
            if (-not (Test-Path -LiteralPath $fullPath)) {
                $errors.Add("Missing frame file '$relPath' referenced by '$displayKey'[$i].")
                continue
            }

            $png = [JiuMiPetPackQaTool]::InspectPng($fullPath)
            if (-not $png.IsValidPng) {
                $errors.Add("Invalid PNG '$relPath': $($png.Error)")
                continue
            }

            if ($png.Width -ne $canvasWidth -or $png.Height -ne $canvasHeight) {
                $errors.Add("Frame '$relPath' is $($png.Width)x$($png.Height); expected ${canvasWidth}x${canvasHeight}.")
            }
            if ($png.BitDepth -ne 8 -or $png.ColorType -ne 6) {
                $errors.Add("Frame '$relPath' is bitDepth=$($png.BitDepth), colorType=$($png.ColorType); expected 8-bit RGBA (8, 6).")
            }
            if ($png.ExtraChunks.Count -gt 0) {
                $errors.Add("Frame '$relPath' contains disallowed PNG metadata chunk(s): $($png.ExtraChunks -join ', ').")
            }

            $ax = if ($f.ContainsKey('anchor') -and $null -ne $f.anchor) { [double]$f.anchor.x } else { $clipAnchorX }
            $ay = if ($f.ContainsKey('anchor') -and $null -ne $f.anchor) { [double]$f.anchor.y } else { $clipAnchorY }

            $metric = [JiuMiPetPackQaTool]::MeasureFrame($displayKey, $i, $relPath, $fullPath, $ax, $ay)
            $clipMetrics.Add($metric)
            $allFrameMetrics.Add($metric)

            $frameSummaries.Add([ordered]@{
                index = $i
                path = $relPath
                anchor = @{ x = $ax; y = $ay }
                opaquePixels = $metric.OpaquePixels
                bbox = @{ minX = $metric.MinX; minY = $metric.MinY; maxX = $metric.MaxX; maxY = $metric.MaxY }
                centroid = @{ x = [Math]::Round($metric.CentroidX, 2); y = [Math]::Round($metric.CentroidY, 2) }
            })
        }

        $isL1 = (-not $Prefix) -and ($l1Clips -contains $clipKey)
        $minL1Match = $null
        $maxFootDeltaY = 0
        $maxCentroidJump = 0.0
        $maxScaleChangePct = 0.0

        if ($clipMetrics.Count -gt 1) {
            if ($isL1) {
                $minL1Match = 100.0
                for ($i = 1; $i -lt $clipMetrics.Count; $i++) {
                    $pct = [JiuMiPetPackQaTool]::ComputeExactMatchPercent($clipMetrics[0].FullPath, $clipMetrics[$i].FullPath)
                    if ($pct -lt $minL1Match) {
                        $minL1Match = $pct
                    }
                    if ($pct -lt $MinL1ExactMatchPct) {
                        $errors.Add(("L1 zero-drift gate failed on '{0}' frame {1}: diff=0 ratio {2:F2}% < {3:F1}%." -f $displayKey, $i, $pct, $MinL1ExactMatchPct))
                    }
                }
            }

            for ($i = 1; $i -lt $clipMetrics.Count; $i++) {
                $prev = $clipMetrics[$i - 1]
                $curr = $clipMetrics[$i]
                $footDelta = [Math]::Abs($curr.MaxY - $prev.MaxY)
                if ($footDelta -gt $maxFootDeltaY) {
                    $maxFootDeltaY = $footDelta
                }

                $dx = $curr.CentroidX - $prev.CentroidX
                $dy = $curr.CentroidY - $prev.CentroidY
                $jump = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
                if ($jump -gt $maxCentroidJump) {
                    $maxCentroidJump = $jump
                }

                $scalePct = if ($prev.BBoxDiagonal -gt 0) {
                    ([Math]::Abs($curr.BBoxDiagonal - $prev.BBoxDiagonal) / $prev.BBoxDiagonal) * 100.0
                } else { 0.0 }
                if ($scalePct -gt $maxScaleChangePct) {
                    $maxScaleChangePct = $scalePct
                }

                if (-not $isL1 -and -not $L1Only) {
                    if ($jump -gt $MaxCentroidJumpPx) {
                        $errors.Add(("Continuity gate failed on '{0}' frame {1}->{2}: centroid jump {3:F2} px > {4:F1} px." -f $displayKey, ($i - 1), $i, $jump, $MaxCentroidJumpPx))
                    }
                    if ($scalePct -gt $MaxBBoxScaleChangePct) {
                        $errors.Add(("Continuity gate failed on '{0}' frame {1}->{2}: bbox scale change {3:F2}% > {4:F1}%." -f $displayKey, ($i - 1), $i, $scalePct, $MaxBBoxScaleChangePct))
                    }
                }
            }

            $onionDir = Join-Path $OutputDir 'onion-skin'
            $safeName = $displayKey.Replace('.', '-')
            [JiuMiPetPackQaTool]::RenderOnionSkin($clipMetrics.ToArray(), (Join-Path $onionDir "$safeName.png"))
        }

        $clipReports[$displayKey] = [ordered]@{
            frameCount = $clipMetrics.Count
            tier = if ($isL1) { 'L1' } else { 'L2/L3' }
            minL1ExactMatchPct = if ($null -ne $minL1Match) { [Math]::Round($minL1Match, 2) } else { $null }
            maxFootDeltaY = $maxFootDeltaY
            maxCentroidJumpPx = [Math]::Round($maxCentroidJump, 2)
            maxBBoxScaleChangePct = [Math]::Round($maxScaleChangePct, 2)
            frames = $frameSummaries
        }
    }
}

Process-ClipCollection -Collection $manifest.clips -Prefix ''
if ($manifest.ContainsKey('interactions') -and $null -ne $manifest.interactions) {
    Process-ClipCollection -Collection $manifest.interactions -Prefix 'interactions'
}

function Get-FirstFrameMetric {
    param([string]$ClipName)
    foreach ($metric in $allFrameMetrics) {
        if ($metric.Clip -eq $ClipName) {
            return $metric
        }
    }
    return $null
}

# Cross-clip L1 base lock check: blink and react derive from lying-idle[0] (head/eye/ear edit only)
$lyingBaseFrame = Get-FirstFrameMetric -ClipName 'lying-idle'
if ($null -ne $lyingBaseFrame) {
    foreach ($derivedClip in @('blink', 'react')) {
        $derivedFrames = @($allFrameMetrics | Where-Object { $_.Clip -eq $derivedClip })
        for ($i = 0; $i -lt $derivedFrames.Count; $i++) {
            $pct = [JiuMiPetPackQaTool]::ComputeExactMatchPercent($lyingBaseFrame.FullPath, $derivedFrames[$i].FullPath)
            if ($pct -lt $MinL1ExactMatchPct) {
                $errors.Add(("L1 base-pose zero-drift gate failed on '{0}' frame {1} vs 'lying-idle'[0]: diff=0 ratio {2:F2}% < {3:F1}%." -f $derivedClip, $i, $pct, $MinL1ExactMatchPct))
            }
        }
    }
}

# Cross-clip L1 zero-drift gate for directional sit-watch interactions (same sitting body/tail, head turn only)
# Ratio 0.495 maps to y >= 198 on a 400x400 canvas, strictly below the chin/neck seam.
$sitWatchKeys = @('interactions.sit-watch-left', 'interactions.sit-watch-center', 'interactions.sit-watch-right')
$sitWatchMap = [ordered]@{}
foreach ($key in $sitWatchKeys) {
    $frameMetric = Get-FirstFrameMetric -ClipName $key
    if ($null -ne $frameMetric) {
        $sitWatchMap[$key] = $frameMetric
    }
}
if ($sitWatchMap.Count -eq 3) {
    $centerFrame = $sitWatchMap['interactions.sit-watch-center']
    $lowerBodyMinY = [int][Math]::Round($canvasHeight * $SitWatchLowerBodyRatio)
    $sitWatchMatchPcts = @{ 'interactions.sit-watch-center' = 100.0 }
    $sitWatchFootDeltaY = 0

    foreach ($sideKey in @('interactions.sit-watch-left', 'interactions.sit-watch-right')) {
        $sideFrame = $sitWatchMap[$sideKey]
        $pct = [JiuMiPetPackQaTool]::ComputeExactMatchPercentBelowY($centerFrame.FullPath, $sideFrame.FullPath, $lowerBodyMinY)
        $sitWatchMatchPcts[$sideKey] = $pct
        $footDelta = [Math]::Abs($sideFrame.MaxY - $centerFrame.MaxY)
        if ($footDelta -gt $sitWatchFootDeltaY) {
            $sitWatchFootDeltaY = $footDelta
        }
        if ($pct -lt $MinSitWatchLowerBodyMatchPct) {
            $errors.Add(("L1 sit-watch lower-body zero-drift gate failed on '{0}' vs center (y>={1}): diff=0 ratio {2:F2}% < {3:F1}%." -f $sideKey, $lowerBodyMinY, $pct, $MinSitWatchLowerBodyMatchPct))
        }
    }
    if ($sitWatchFootDeltaY -gt 0) {
        $errors.Add(("L1 sit-watch foot alignment gate failed: foot_dy={0}px across sit-watch directions (expected 0px)." -f $sitWatchFootDeltaY))
    }

    $onionDir = Join-Path $OutputDir 'onion-skin'
    [JiuMiPetPackQaTool]::RenderOnionSkin([PetFrameMetrics[]]@($sitWatchMap.Values), (Join-Path $onionDir 'interactions-sit-watch.png'))

    foreach ($key in $sitWatchKeys) {
        if ($clipReports.Contains($key)) {
            $clipReports[$key]['tier'] = 'L1'
            $clipReports[$key]['minL1ExactMatchPct'] = [Math]::Round($sitWatchMatchPcts[$key], 2)
            $clipReports[$key]['maxFootDeltaY'] = $sitWatchFootDeltaY
        }
    }
}

# Verify optional SHA-256 files table
if ($manifest.ContainsKey('files') -and $null -ne $manifest.files) {
    foreach ($rel in $manifest.files.Keys) {
        $expectedHash = ([string]$manifest.files[$rel]).Trim()
        $fullPath = Resolve-SafePackPath -RelativePath $rel
        if ($null -eq $fullPath) {
            $errors.Add("Checksum entry '$rel' has an unsafe or invalid relative path.")
            continue
        }
        if (-not (Test-Path -LiteralPath $fullPath)) {
            $errors.Add("Checksum entry '$rel' does not exist.")
            continue
        }

        $actualHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
        if (-not $actualHash.Equals($expectedHash, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("SHA-256 mismatch for '$rel': expected $expectedHash, actual $actualHash.")
        }
    }
}

# Detect orphan PNGs
$diskPngs = Get-ChildItem -LiteralPath $resolvedPackDir -Recurse -Filter '*.png' -File
$orphans = [System.Collections.Generic.List[string]]::new()
foreach ($file in $diskPngs) {
    $rel = [System.IO.Path]::GetRelativePath($resolvedPackDir, $file.FullName).Replace('\', '/')
    if (-not $referencedPaths.Contains($rel)) {
        $orphans.Add($rel)
    }
}

if ($orphans.Count -gt 0) {
    $msg = "Found $($orphans.Count) orphan PNG file(s) not referenced by manifest.json (first: $($orphans[0]))."
    if ($AllowOrphans) {
        $warnings.Add($msg)
    } else {
        $errors.Add($msg)
    }
}

# Render Contact Sheet and Light/Dark 200 DIP preview
$contactSheetPath = Join-Path $OutputDir 'contact-sheet.png'
if ($allFrameMetrics.Count -gt 0) {
    [JiuMiPetPackQaTool]::RenderContactSheet($allFrameMetrics.ToArray(), $contactSheetPath)
}

$sampleOrder = @(
    'lying-idle',
    'blink',
    'react',
    'sleep',
    'drag',
    'interactions.sit-watch-left',
    'interactions.sit-watch-center',
    'interactions.sit-watch-right',
    'walk',
    'stretch',
    'jump'
)
$sampleFrames = [System.Collections.Generic.List[PetFrameMetrics]]::new()
foreach ($sampleKey in $sampleOrder) {
    $match = Get-FirstFrameMetric -ClipName $sampleKey
    if ($null -ne $match) {
        $sampleFrames.Add($match)
    }
}
$previewPath = Join-Path $OutputDir 'preview-light-dark-200dip.png'
if ($sampleFrames.Count -gt 0) {
    [JiuMiPetPackQaTool]::RenderLightDarkPreview($sampleFrames.ToArray(), $previewPath)
}

$report = [ordered]@{
    packId = $packId
    packDir = $resolvedPackDir
    canvas = @{ width = $canvasWidth; height = $canvasHeight }
    defaultAnchor = @{ x = $defaultAnchorX; y = $defaultAnchorY }
    frameCount = $allFrameMetrics.Count
    orphanCount = $orphans.Count
    passed = ($errors.Count -eq 0)
    errors = $errors
    warnings = $warnings
    clips = $clipReports
    artifacts = @{
        contactSheet = $contactSheetPath
        lightDarkPreview = $previewPath
        onionSkinDir = (Join-Path $OutputDir 'onion-skin')
    }
}

$reportPath = Join-Path $OutputDir 'report.json'
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Pet Pack QA Summary: $packId ($($allFrameMetrics.Count) frames)"
Write-Host ("  Canvas: {0}x{1}  DefaultAnchor: ({2}, {3})" -f $canvasWidth, $canvasHeight, $defaultAnchorX, $defaultAnchorY)
foreach ($key in $clipReports.Keys) {
    $c = $clipReports[$key]
    $l1Text = if ($null -ne $c.minL1ExactMatchPct) { "L1_diff0=$($c.minL1ExactMatchPct)%" } else { "L1_diff0=n/a" }
    Write-Host ("  {0,-14} frames={1,-2} {2,-16} foot_dy={3,-2}px  centroid_jump={4,-5}px  scale_chg={5}%" -f `
        $key, $c.frameCount, $l1Text, $c.maxFootDeltaY, $c.maxCentroidJumpPx, $c.maxBBoxScaleChangePct)
}
Write-Host "  Artifacts: $OutputDir"
Write-Host "  Warnings:  $($warnings.Count)   Errors: $($errors.Count)"

if ($warnings.Count -gt 0) {
    Write-Host ''
    Write-Host 'Warnings:' -ForegroundColor Yellow
    foreach ($w in $warnings) { Write-Host "  - $w" -ForegroundColor Yellow }
}
if ($errors.Count -gt 0) {
    Write-Host ''
    Write-Host 'Errors:' -ForegroundColor Red
    foreach ($e in $errors) { Write-Host "  - $e" -ForegroundColor Red }
}

if ($errors.Count -gt 0 -and -not $ReportOnly) {
    exit 1
}

exit 0
