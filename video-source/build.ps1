winget install --id=Gyan.FFmpeg

Clear-Host

Remove-Item ../video/*.mp4 
Remove-Item ../video/*.gif 

function Convert-JpegsToGif {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceDir,
        [Parameter(Mandatory = $true)]
        [string]$OutputDir,
        [Parameter(Mandatory = $true)]
        [string]$OutputFileName,
        [Parameter(Mandatory = $true)]
        [float]$FramesPerSecond
    )
    
    $inputPattern = Join-Path $SourceDir "%02d.jpeg"
    $outputMp4 = Join-Path $OutputDir "$OutputFileName.mp4"
    $outputGif = Join-Path $OutputDir "$OutputFileName.gif"

    Write-Output xx $inputPattern
    echo xx $outputMp4
    echo xx $outputGif

    ffmpeg -framerate $FramesPerSecond -i $inputPattern -c:v libvpx-vp9 -lossless 1 -vf "scale=1024:768" -y $outputMp4
    $palette = "palette.png"
    $filters = "fps=12,scale=1072:-1:flags=lanczos"
    ffmpeg -v warning -i $outputMp4 -vf "$filters,palettegen" -y $palette
    ffmpeg -v warning -i $outputMp4 -i $palette -lavfi "$filters [x]; [x][1:v] paletteuse" -y $outputGif
    Remove-Item $palette
    Remove-Item $outputMp4
}

Convert-JpegsToGif -SourceDir "welcome" -OutputDir "../video" -OutputFileName "welcome" -FramesPerSecond 0.5