# WebRTC Snapshot Example
This is some example code for connecting to a WHEP endpoint and saving a snapshot of a frame, written in C# using .NET 8. It was written to be used in connection with [Broadcast Box](https://github.com/glimesh/broadcast-box), but could be repurposed for any other WHEP endpoint.

## Run this locally
To run this locally you will be required to make some changes to this program to target the correct OS, FFMPEG library, and stream.

### Clone
Clone this repo and navigate to the program directory
```sh
git clone https://github.com/richardtatum/webrtc-snapshot-example.git webrtc-snapshot-example
cd webrtc-snapshot-example/Snapshot
```

### Update variables
Update the `url`, `streamKey`, `imageFileName` and `ffmpegLibLocation` variables as required
```diff
# Program.cs
- var url = "https://b.siobud.com/api/whep"; 
+ var url = "https://hosted.broadcast.box/api/whep";

- var streamKey = "stream-key";
+ var streamKey = "richard-test";

- var imageFileName = $"/path/to/frame-{streamKey}.jpg"
+ var imageFileName = $"/home/rt/Pictures/snapshot/frame-{streamKey}.jpg"

- var ffmpegLibLocation = "/usr/lib64";
+ var ffmpegLibLocation = "/usr/lib/x86_64-linux-gnu";
```

### Run
You can then run the program with dotnet run:
```sh
dotnet run
```

After starting, if everything is setup correctly, you will see an output similar to the following:
```sh
[PROGRAM] Connecting to WHEP endpoint...
[CONNECTION] Answer received
[CONNECTION] ICE state changed: checking
[CONNECTION] Remote description set. Response: OK
[PROGRAM] Press any key to exit...
[CONNECTION] ICE state changed: connected
[CONNECTION] State changed: connecting
[CONNECTION] State changed: connected
```

You can then start your stream through OBS/streaming software of choice, ensuring that the provided stream key matches the one setup in [Update Variables](#update-variables):
```sh
[STREAM] Frame received. Size: 2024x848 Format: Rgb
[PROGRAM] Frame saved to file. Path: /home/rt/Pictures/snapshot/frame-richard-test.jpg
```

By default, this will continue to override the image every 30 seconds:
```sh
[TIMER] Snapshot timer reset.
[STREAM] Frame received. Size: 2024x848 Format: Rgb
[PROGRAM] Frame saved to file. Path: /home/rt/Pictures/snapshot/frame-richard-test.jpg
```

## Troubleshooting
Below are some troubleshooting steps that may help if you have issues
- If the stream does not connect, ensure the url and key are set correctly
- If the SDP offer is rejected, it is possible you have set an incorrect VideoCodec for the endpoint in question
- If FFMPEG cannot be initialised, make sure you are not setting it as the location of the executable, but the libs. For Arch linux ffmpeg lives at `/sbin/ffmpeg` but the lib is at `/usr/lib64`
- If you are using Windows or MacOS and can't get FFMPEG initialised, check the issues over at [SIPSorceryMedia.FFMPEG](https://github.com/sipsorcery-org/SIPSorceryMedia.FFmpeg/issues)
- If the saved image quality is poor, you can increase it by setting the `Quality` config variable in `SaveFrameToFile > image.SaveAsJpeg`. This is line `155` by default. 
