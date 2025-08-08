winget install --id=Gyan.FFmpeg

Clear-Host

Remove-Item ../video/*.mp4 
Remove-Item ../video/*.gif 

function Convert-JpegsToGif {
    param(
        [string]$FileName,
        [float]$FramesPerSecond
    )
    
    ffmpeg -framerate $FramesPerSecond -i $FileName/%02d.jpeg -c:v libvpx-vp9 -lossless 1 -vf "scale=1024:768" -y ../video/$FileName.mp4
    $palette = "palette.png"
    $filters = "fps=12,scale=1072:-1:flags=lanczos"
    ffmpeg -v warning -i ../video/$FileName.mp4 -vf "$filters,palettegen" -y $palette
    ffmpeg -v warning -i ../video/$FileName.mp4 -i $palette -lavfi "$filters [x]; [x][1:v] paletteuse" -y ../video/$FileName.gif
    Remove-Item $palette
    Remove-Item ../video/$FileName.mp4
}

Convert-JpegsToGif -FileName "welcome" -FramesPerSecond 0.5