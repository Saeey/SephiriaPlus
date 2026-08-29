param(
    [string]$ProjectRoot = "D:\个人项目\赛菲莉娅mod"
)

$ErrorActionPreference = "Stop"
$pythonExe = "C:\Users\null\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe"
$env:PYTHONPATH = "$ProjectRoot\.tools\pydeps"

& $pythonExe "$ProjectRoot\tools\make_intro_video.py"

$ffmpeg = (Get-ChildItem -LiteralPath "$ProjectRoot\.tools\pydeps\imageio_ffmpeg\binaries" -Filter "ffmpeg*.exe" | Select-Object -First 1).FullName
if (-not $ffmpeg) { throw "FFmpeg executable not found" }
$videoDir = Join-Path $ProjectRoot "video"
$audioDir = Join-Path $videoDir "audio"
$segmentDir = Join-Path $videoDir "segments"
New-Item -ItemType Directory -Force -Path $audioDir, $segmentDir | Out-Null

Add-Type -AssemblyName System.Speech
$slides = Get-Content -LiteralPath (Join-Path $videoDir "script.json") -Raw | ConvertFrom-Json
$concatLines = New-Object System.Collections.Generic.List[string]

for ($i = 0; $i -lt $slides.Count; $i++) {
    $n = $i + 1
    $wav = Join-Path $audioDir ("voice_{0:D2}.wav" -f $n)
    $png = Join-Path $videoDir ("slides\slide_{0:D2}.png" -f $n)
    $segment = Join-Path $segmentDir ("segment_{0:D2}.mp4" -f $n)

    $synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
    $synth.SelectVoice("Microsoft Huihui Desktop")
    $synth.Rate = 1
    $synth.Volume = 100
    $synth.SetOutputToWaveFile($wav)
    $synth.Speak([string]$slides[$i].voice)
    $synth.Dispose()

    $duration = & $pythonExe -c "import wave; w=wave.open(r'$wav','rb'); print(w.getnframes()/w.getframerate()+1.2); w.close()"
    & $ffmpeg -y -loop 1 -framerate 30 -i $png -i $wav `
        -vf "scale=1920:1080:flags=lanczos,format=yuv420p" `
        -c:v libx264 -preset medium -crf 19 -r 30 -c:a aac -b:a 192k `
        -af "apad=pad_dur=1.2" -t $duration -movflags +faststart $segment
    if ($LASTEXITCODE -ne 0) { throw "FFmpeg failed for segment $n" }
    $concatLines.Add("file '$($segment.Replace("'", "''"))'")
}

$concatFile = Join-Path $videoDir "concat.txt"
$concatLines | Set-Content -LiteralPath $concatFile -Encoding utf8
$output = Join-Path $ProjectRoot "dist\SephiriaPlus-v2.2.4-介绍与安装.mp4"
& $ffmpeg -y -f concat -safe 0 -i $concatFile -c copy -movflags +faststart $output
if ($LASTEXITCODE -ne 0) { throw "Final FFmpeg concat failed" }

Write-Output $output
