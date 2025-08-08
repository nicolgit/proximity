winget install --id=Gyan.FFmpeg

Clear-Host

Remove-Item ../video/*.mp4 

ffmpeg -framerate 0.5 -i welcome/%02d.jpeg -c:v libvpx-vp9 -lossless 1 -y ../video/welcome.mp4
