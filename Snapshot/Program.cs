using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

var url = "https://b.siobud.com/api/whep"; // This is the WHEP endpoint to connect to
var streamKey = "stream-key"; // This is the stream key you set in OBS
var imageFileName = $"/path/to/frame-{streamKey}.jpg"; // This is where you want to save the final image

// This is the location of the ffmpeg library. Confusingly not the output of `where ffmpeg`. This will need changing depending on your OS
// For Arch/Fedora-based systems its '/usr/lib64', for Debian '/usr/lib/x86_64-linux-gnu/'
// For Windows it may be something like 'C:\ffmpeg-4.4.1-full_build-shared\bin'
var ffmpegLibLocation = "/usr/lib64";

var takeSnapshot = true;
var snapshotFrequency = 30000;

// Every X seconds, set `takeSnapshot` back to true to initiate grabbing a new frame
StartSnapshotTimer(snapshotFrequency);

// The video endpoint handles the behaviour of receiving a decoded frame (save it to file)
var videoEndpoint = SetupVideoEndpoint(OnFrameReceived);

// The peer connection handles connecting to the stream, and what to happen when receiving a frame (trigger video endpoint)
var peerConnection = SetupPeerConnection(videoEndpoint);

// The SDP offer comprises information about the ICE candidates, supported network information, security parameters and supported codecs
var offer = peerConnection.createOffer();
await peerConnection.setLocalDescription(offer);

// The SDP offer is sent to the WHEP endpoint to establish a connection
Console.WriteLine("[PROGRAM] Connecting to WHEP endpoint...");
var answer = await SendOfferAsync(url, offer.sdp, streamKey);
if (answer is null)
{
    Console.WriteLine("[PROGRAM] Shutting down.");
    return;
}

// If the connection is successful, we use the answer to set the remote description
var result = peerConnection.setRemoteDescription(new RTCSessionDescriptionInit
{
    type = RTCSdpType.answer, sdp = answer
});
Console.WriteLine($"[CONNECTION] Remote description set. Response: {result}");

// Start the connection. This can happen before the stream is live.
await peerConnection.Start();

// Keep the application running.
// When the application is running and the stream is live, it will begin capturing frames and saving them to file
Console.WriteLine("[PROGRAM] Press any key to exit...");
Console.ReadKey();
return;

void OnFrameReceived(RawImage image)
{
    if (!takeSnapshot)
    {
        return;
    }

    Console.WriteLine($"[STREAM] Frame received. Size: {image.Width}x{image.Height} Format: {image.PixelFormat}");
    SaveFrameToFile(imageFileName, image.Sample, image.Width, image.Height, image.Stride);
    takeSnapshot = false;
}

void StartSnapshotTimer(double interval)
{
    var saveTimer = new System.Timers.Timer(interval);
    saveTimer.AutoReset = true;
    saveTimer.Enabled = true;
    saveTimer.Elapsed += (_, _) =>
    {
        Console.WriteLine("[TIMER] Snapshot timer reset.");
        takeSnapshot = true;
    };
}

static async Task<string?> SendOfferAsync(string url, string offerSdp, string bearerToken)
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/sdp"));
    var content = new StringContent(offerSdp, Encoding.UTF8, "application/sdp");

    var response = await client.PostAsync(url, content);
    if (!response.IsSuccessStatusCode)
    {
        Console.WriteLine(
            $"[CONNECTION] Offer request failed. StatusCode: {response.StatusCode}, Reason: {response.ReasonPhrase}");
        return null;
    }

    Console.WriteLine("[CONNECTION] Answer received");
    return await response.Content.ReadAsStringAsync();
}

FFmpegVideoEndPoint SetupVideoEndpoint(VideoSinkSampleDecodedFasterDelegate videoSinkSampleDecodedFasterDelegate)
{
    // Initialise FFMPEG for extracting frame information with SIPSorcery
    FFmpegInit.Initialise(FfmpegLogLevelEnum.AV_LOG_FATAL, libPath: ffmpegLibLocation);
    var endpoint = new FFmpegVideoEndPoint();
    endpoint.RestrictFormats(format => format.Codec == VideoCodecsEnum.H264);
    endpoint.OnVideoSinkDecodedSampleFaster += videoSinkSampleDecodedFasterDelegate;

    return endpoint;
}

RTCPeerConnection SetupPeerConnection(FFmpegVideoEndPoint endpoint)
{
    // Initialize the PeerConnection.
    var pc = new RTCPeerConnection(new RTCConfiguration
    {
        iceServers = [new RTCIceServer { urls = "stun:stun.cloudflare.com:3478" }],
        X_UseRtpFeedbackProfile = true
    });

    // Add the tracks we are accepting. Currently only H264 as specified in `GetVideoEndpoint()`  
    pc.addTrack(new MediaStreamTrack(endpoint.GetVideoSinkFormats(), MediaStreamStatusEnum.RecvOnly));
    pc.OnVideoFormatsNegotiated += (formats) => endpoint.SetVideoSinkFormat(formats.First());

    // Trigger `endpoint.OnVideoSinkDecodedSampleFaster` on every received frame
    pc.OnVideoFrameReceived += endpoint.GotVideoFrame;

    // Debug information about state changes
    pc.onconnectionstatechange += (state) => Console.WriteLine($"[CONNECTION] State changed: {state}");
    pc.oniceconnectionstatechange += (state) => Console.WriteLine($"[CONNECTION] ICE state changed: {state}");

    return pc;
}

void SaveFrameToFile(string fileName, IntPtr ptr, int width, int height, int stride)
{
    var bytesPerPixel = 3; // Assuming Rgba24 format
    var totalBytes = height * stride; // Calculate total bytes in the image
    var pixelData = new byte[totalBytes];

    Marshal.Copy(ptr, pixelData, 0, totalBytes);

    // The data is in BGR format it needs to be converted to RGB
    pixelData = SwapRedBlueChannels(width, height, stride, bytesPerPixel, pixelData);

    // Create ImageSharp image from pixel data
    var image = Image.LoadPixelData<Rgb24>(pixelData, width, height);

    // Save the image to the specified file path as a JPEG
    image.SaveAsJpeg(fileName, new JpegEncoder
    {
        Quality = 80 // Quality can be adjusted between 0 (lowest) and 100 (highest)
    });
    
    Console.WriteLine($"[PROGRAM] Frame saved to file. Path: {fileName}");
}

static byte[] SwapRedBlueChannels(int width, int height, int stride, int bytesPerPixel, byte[] pixelData)
{
    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            var index = y * stride + x * bytesPerPixel;

            // Swap BGR to RGB
            (pixelData[index], pixelData[index + 2]) = (pixelData[index + 2], pixelData[index]);
        }
    }

    return pixelData;
}