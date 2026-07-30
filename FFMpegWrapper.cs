/*
 *  MixOptimize - C&C Renegade map and mod package optimizer
 *  Copyright (C) 2026 Unstoppable
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System.Net.Sockets;

namespace mixoptimize;

public struct FFProbeResult
{
    public string Format { get; set; }
    public string Codec { get; set; }
    public int BitRate { get; set; }
}

public static class FFMpegWrapper
{
    /// <summary>
    /// Determines whether 'ffmpeg' is installed in the system.
    /// </summary>
    public static readonly Lazy<bool> Installed = new(FFMpegInstalled);

    public static string FFMpegPath = Path.Combine(Path.GetTempPath(), "ffmpeg");

    public static FFProbeResult ProbeFile(Stream input)
    {
        ExecuteFFProbe("-select_streams a:0 -show_entries format=format_name:stream=codec_name,bit_rate -of csv=p=0", input, out var output);
        var tokens = output.Split([Environment.NewLine, ","], StringSplitOptions.RemoveEmptyEntries);
        
        return new FFProbeResult
        {
            Codec = tokens[0],
            BitRate = int.Parse(tokens[1]),
            Format = tokens[2]
        };
    }

    public static byte[] TranscodeFile(byte[] input, string format)
    {
        using var inputStream = new MemoryStream(input);
        using var outputStream = new MemoryStream();
        
        ExecuteFFMpeg("-map 0:a:0 -map_metadata -1 -vn -ar 44100 -ac 2 -c:a libmp3lame -b:a 128k -joint_stereo 0 -write_xing 0 -id3v2_version 0 -f mp3", format, inputStream, outputStream);
        
        return outputStream.ToArray();
    }
    
    private static async Task CreateProcessAsync(string name, string arguments, Stream input, Stream output)
    {
        using Process? proc = new Process();
        proc.StartInfo = new ProcessStartInfo(name)
        {
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        
        if (proc is null)
        {
            throw new InvalidOperationException($"Could not start {name}.");    
        }

        if (input.CanSeek)
        {
            input.Seek(0, SeekOrigin.Begin);
        }

        Task stdinTask, stdoutTask;
        Task<string> stderrTask;
        
        if (!proc.Start())
            throw new Exception($"Could not launch {name}.");

        stdinTask = Task.Run(async () =>
        {
            // If we're running ffprobe, it stops consuming input once it has enough data to figure out the file.
            // This situation may cause CopyTo to throw an exception in certain systems.
            try
            {
                await input.CopyToAsync(proc.StandardInput.BaseStream);
                await proc.StandardInput.BaseStream.FlushAsync();
            }
            catch (IOException)
            {
                // Ignored, depends on exit code.
            }
            finally
            {
                try
                {
                    proc.StandardInput.Close();
                }
                catch (Exception)
                {
                    // This sometimes throws if process exits before this line executes.
                }
            }
        });

        stdoutTask = proc.StandardOutput.BaseStream.CopyToAsync(output);
        stderrTask = proc.StandardError.ReadToEndAsync();

        await stdinTask;
        await proc.WaitForExitAsync();
        await stdoutTask;
        
        if (proc.ExitCode != 0)
        {
            throw new Exception($"{name} exited with non-success status code." + Environment.NewLine + await stderrTask);
        }
    }
    
    private static void ExecuteFFMpeg(string arguments, string format, Stream input, Stream output)
    {
        const string exec = "ffmpeg";
        var path = Installed.Value ? exec : Path.Combine(FFMpegPath, "bin", exec);
        
        CreateProcessAsync(path, $"-hide_banner -v error -f {format} -i pipe:0 {arguments} pipe:1", input, output).GetAwaiter().GetResult();
    }
    
    private static void ExecuteFFProbe(string arguments, Stream input, out string output)
    {
        using var ms = new MemoryStream();
        using var sr = new StreamReader(ms);
        
        const string exec = "ffprobe";
        var path = Installed.Value ? exec : Path.Combine(FFMpegPath, "bin", exec);
        
        CreateProcessAsync(path, $"-hide_banner -v error -i pipe:0 {arguments}", input, ms).GetAwaiter().GetResult();;
        
        ms.Seek(0, SeekOrigin.Begin);
        output = sr.ReadToEnd();
    }
    
    private static bool FFMpegInstalled()
    {
        try
        {
            var proc = new Process();
            proc.StartInfo = new ProcessStartInfo("ffmpeg")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            if (!proc.Start())
            {
                return false;
            }

            proc.WaitForExit();
        }
        catch (Exception)
        {
            return false;
        }
        
        return true;
    }
}