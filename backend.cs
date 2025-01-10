using System.Diagnostics;
using System.IO;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;

class Program
{
    private static WasapiLoopbackCapture _audioCapture;
    private static WaveFileWriter _waveWriter;
    private static readonly string _recordingsDir = Path.Combine(AppContext.BaseDirectory, "recordings");
// private static readonly string _recordingsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recordings");

static void Main(string[] args)
{
    Console.WriteLine($"Using recordings directory: {_recordingsDir}");
    Directory.CreateDirectory(_recordingsDir);

    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddCors();
    var app = builder.Build();
    app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    app.MapPost("/record", async context =>
    {
        try
        {
            string voice = context.Request.Query["voice"];
            if (string.IsNullOrWhiteSpace(voice))
            {
                voice = "UnknownVoice"; // Fallback if no voice is provided
            }
            // Sanitize the voice name for use in filenames
            string sanitizedVoice = string.Concat(voice.Split(Path.GetInvalidFileNameChars()));
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string filename = Path.Combine(_recordingsDir, $"{sanitizedVoice}_{timestamp}.wav");
            Console.WriteLine($"Starting recording. File will be saved to: {filename}");

            using var enumerator = new MMDeviceEnumerator();
            var audioDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            _audioCapture = new WasapiLoopbackCapture(audioDevice);
            _waveWriter = new WaveFileWriter(filename, _audioCapture.WaveFormat);

            _audioCapture.DataAvailable += (s, e) =>
            {
                _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
            };

            _audioCapture.RecordingStopped += (s, e) =>
            {
                _waveWriter?.Dispose();
                _audioCapture?.Dispose();
            };

            _audioCapture.StartRecording();
            await context.Response.WriteAsync("Recording started.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting recording: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Failed to start recording.");
        }
    });

    app.MapPost("/stop", async context =>
    {
        try
        {
            _audioCapture?.StopRecording();
            Console.WriteLine("Recording stopped.");
            await context.Response.WriteAsync("Recording stopped.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error stopping recording: {ex.Message}");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync("Failed to stop recording.");
        }
    });

    app.MapGet("/recordings", async context =>
    {
        Console.WriteLine($"Listing files in: {_recordingsDir}");
        var recordings = Directory.EnumerateFiles(_recordingsDir, "*.wav")
                                  .Select(Path.GetFileName)
                                  .ToList();
        await context.Response.WriteAsJsonAsync(recordings);
    });

    app.MapGet("/recordings/{filename}", async context =>
    {
        var filename = context.Request.RouteValues["filename"]?.ToString();
        if (string.IsNullOrEmpty(filename))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Filename not specified.");
            return;
        }

        var filepath = Path.Combine(_recordingsDir, filename);
        if (File.Exists(filepath))
        {
            Console.WriteLine($"Sending file: {filepath}");
            context.Response.ContentType = "audio/wav";
            await context.Response.SendFileAsync(filepath);
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("File not found.");
        }
    });

    app.MapDelete("/recordings/{filename}", async context =>
    {
        var filename = context.Request.RouteValues["filename"]?.ToString();
        if (string.IsNullOrEmpty(filename))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Filename not specified.");
            return;
        }

        var filepath = Path.Combine(_recordingsDir, filename);
        if (File.Exists(filepath))
        {
            Console.WriteLine($"Deleting file: {filepath}");
            File.Delete(filepath);
            await context.Response.WriteAsync("Recording deleted.");
        }
        else
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("File not found.");
        }
    });

    app.Run("http://localhost:5000");
}
}