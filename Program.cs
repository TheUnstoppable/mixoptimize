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

namespace mixoptimize;

public class MixOptimizeHelpProvider : HelpProvider
{
    public MixOptimizeHelpProvider(ICommandAppSettings settings) : base(settings) { }

    public override IEnumerable<IRenderable> GetFooter(ICommandModel model, ICommandInfo? command)
    {
        var list = base.GetFooter(model, command).ToList();
        
        list.Add(new Rule());
        list.Add(new Markup($"[blue]{MixOptimize.Name}[/] is licensed under [bold]GNU General Public License v3.0[/]. Please view [b]LICENSE[/] file for details." + Environment.NewLine));
        list.Add(new Markup(Environment.NewLine));
        list.Add(new Markup($"[blue]{MixOptimize.Name}[/] uses the following open-source libraries:" + Environment.NewLine));
        list.Add(new Rows(
            new Markup("[bold]FFmpeg[/] [dim]by[/] FFmpeg Contributors"),
            new Markup("[bold]Magick.NET[/] [dim]by[/] Dirk Lemstra"),
            new Markup("[bold]MixLibrary[/] [dim]by[/] The Unstoppable"),
            new Markup("[bold]SharpCompress[/] [dim]by[/] Adam Hathcock"),
            new Markup("[bold]Spectre.Console.Cli[/] [dim]by[/] Patrik Svensson, Phil Scott, Nils Andresen, Cédric Luthi")
        ));
        list.Add(new Rule());

        return list;
    }
}

public class MixOptimizeSettings : CommandSettings
{
    [CommandArgument(0, "[input]")]
    [Description("The input file. Ignored if '--read-stdin' is specified.")]
    public string? InputFile { get; set; } = null;
    
    [CommandOption("--skip-texture-conversion")]
    [Description("Skips texture conversion to DDS.")]
    [DefaultValue(false)]
    public bool SkipTextureConversion { get; set; }
    
    [CommandOption("--skip-texture-resize")]
    [Description("Skips resizing textures to a square.")]
    [DefaultValue(false)]
    public bool SkipTextureResize { get; set; }
    
    [CommandOption("--skip-sounds")]
    [Description("Skips re-encoding sounds to MP3 @ 128 kbps.")]
    [DefaultValue(false)]
    public bool SkipSounds { get; set; }
    
    [CommandOption("--read-stdin")]
    [Description("Reads the file from standard input instead. (Implies --skip-confirmation)")]
    [DefaultValue(false)]
    public bool ReadStandardInput { get; set; }
    
    [CommandOption("--skip-confirmation")]
    [Description("Skips confirmation for the changes to be done.")]
    [DefaultValue(false)]
    public bool SkipConfirmation { get; set; }
    
    [CommandOption("--max-exponent")]
    [Description("The maximum power of two to use while resizing. (Default: 9 -> 2^9 = 512)")]
    [DefaultValue(9)]
    public int MaxExponent { get; set; }
    
    [CommandOption("--ffmpeg-location")]
    [Description($"Path which contains the 'ffmpeg' binary. If not specified and not found in the system, {MixOptimize.Name} will attempt to download it.")]
    public string? FFMpegLocation { get; set; } = null;

    [CommandOption("--out")]
    [Description("Output file. (Required if --read-stdin is specified)")]
    public string? OutputFile { get; set; } = null;
}

public class MixOptimizeCommand : Command<MixOptimizeSettings>
{
    private Lazy<int> _ActionCount => new(() => Results.Count(x => x.Value.NeedsAction));
    
    private MixPackageClass MixFile { get; set; }
    private Dictionary<int, IAnalysisResult> Results { get; set; }
    private int ActionCount => _ActionCount.Value;
    
    private int EnsureFFMpegExists(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken)
    {
        var file = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        
        if (!string.IsNullOrWhiteSpace(settings.FFMpegLocation))
        {
            if (Path.Exists(Path.Combine(settings.FFMpegLocation, file)))
            {
                AnsiConsole.MarkupLine("Using [bold]ffmpeg[/] from provided location...");
                FFMpegWrapper.FFMpegPath = settings.FFMpegLocation;
                return 0;
            }
            else
            {
                AnsiConsole.ErrorLine("ffmpeg could not be found in the specified location.");
                return -5;
            }
        }
        
        if (FFMpegWrapper.Installed.Value)
        {
            AnsiConsole.MarkupLine("Using [bold]ffmpeg[/] from system...");
            return 0;
        }

        if (!Path.Exists(FFMpegWrapper.FFMpegPath))
        {
            Directory.CreateDirectory(FFMpegWrapper.FFMpegPath);
        }
        
        if (Path.Exists(Path.Combine(FFMpegWrapper.FFMpegPath, "bin", file)))
        {
            AnsiConsole.MarkupLine("Using [bold]ffmpeg[/] from previously downloaded location...");
            return 0;
        }
        
        AnsiConsole.MarkupLine("[bold]ffmpeg[/] was not found in the system. Downloading...");
        
        string arch;
        switch (RuntimeInformation.ProcessArchitecture)
        {
            case Architecture.Arm64: arch = "arm64"; break;
            case Architecture.X64: arch = "64"; break;
            default: throw new Exception($"Current CPU architecture is not supported to automatically download ffmpeg. Please install an appropriate version of ffmpeg on your system and re-run {MixOptimize.Name}.");
        }

        string name;
        string url = $"https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/";    
        if (OperatingSystem.IsWindows())
            name = $"ffmpeg-master-latest-win{arch}-gpl.zip";
        else if (OperatingSystem.IsLinux())
            name = $"ffmpeg-master-latest-linux{arch}-gpl.tar.xz";
        else
            throw new Exception($"Current operating system is not supported to automatically download ffmpeg. Please install an appropriate version of ffmpeg on your system and re-run {MixOptimize.Name}.");

        string currentFile = string.Empty;
        
        AnsiConsole.Progress()
            .Columns(new SpinnerColumn(), new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
            .AutoClear(true)
            .UseRenderHook((renderable, tasks) =>
            {
                var task = tasks.FirstOrDefault();

                if (task is null)
                    return renderable;

                return string.IsNullOrWhiteSpace(currentFile) ? renderable : new Rows(renderable, new Markup($"[bold]Processing:[/] {Markup.Escape(currentFile)}"));
            })
            .StartAsync(async ctx =>
            {
                var downloadTask = ctx.AddTask("Requesting...");
                downloadTask.IsIndeterminate = true;
                
                var extractTask = ctx.AddTask("Extracting...");
                extractTask.IsIndeterminate = true;
                
                using var client = new HttpClient();
                using var response = await client.GetAsync(url + name, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                downloadTask.MaxValue = response.Content.Headers.ContentLength ?? 0;
 
                var destination = new MemoryStream();
                using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                
                downloadTask.Description = "Downloading...";
                downloadTask.IsIndeterminate = false;
 
                int bytesRead;
                var buffer = new byte[81920];
                while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        AnsiConsole.ErrorLine("Download cancelled.");
                        return;
                    }
                    
                    await destination.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    downloadTask.Increment(bytesRead);
                }
 
                destination.Position = 0;

                using var archive = ReaderFactory.OpenReader(destination, new ReaderOptions()
                {
                    Progress = new Progress<ProgressReport>(x =>
                    {
                        currentFile = x.EntryPath;
                        extractTask.IsIndeterminate = false;
                        extractTask.Description = $"Extracting...";
                        extractTask.Value = x.BytesTransferred;
                        extractTask.MaxValue = x.TotalBytes ?? 0;
                    })
                });
                
                archive.WriteAllToDirectory(FFMpegWrapper.FFMpegPath, new ExtractionOptions { Overwrite = true });

                currentFile = string.Empty;
                extractTask.Description = "Moving...";
                extractTask.IsIndeterminate = true;
                
                var path = Path.Combine(FFMpegWrapper.FFMpegPath, name[..name.IndexOf('.')]);
                Directory.MoveAll(path, FFMpegWrapper.FFMpegPath);
                
                if (OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(Path.Combine(FFMpegWrapper.FFMpegPath, "bin", "ffmpeg"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    File.SetUnixFileMode(Path.Combine(FFMpegWrapper.FFMpegPath, "bin", "ffprobe"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                    File.SetUnixFileMode(Path.Combine(FFMpegWrapper.FFMpegPath, "bin", "ffplay"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
                
                downloadTask.HideWhenCompleted = true;
                extractTask.HideWhenCompleted = true;
                extractTask.IsIndeterminate = false;
            }).GetAwaiter().GetResult();
        
        if (cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.ErrorLine("Aborting...");
            return 1;
        }

        return 0;
    }
    
    private int ReadMixFile(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken, out byte[] result)
    {
        result = null!;
        
        if (settings.ReadStandardInput)
        {
            if (settings.OutputFile == null)
            {
                AnsiConsole.ErrorLine("--out switch has to be specified when --read-stdin is used!");
                return -1;
            }

            settings.SkipConfirmation = true;
            settings.InputFile = "STDIN";

            using (var stdin = Console.OpenStandardInput())
            {
                byte[]? bytes = null;
                AnsiConsole.Status()
                    .Start("Reading the file from standard input...", async x =>
                    {
                        var result = await stdin.ReadAsByteArrayAsync();
                        bytes = result;
                    })
                    .GetAwaiter().GetResult();
                
                result = bytes ?? [];
            }
        }
        else
        {
            if (File.Exists(settings.InputFile))
            {
                settings.OutputFile = settings.InputFile;
                result = File.ReadAllBytes(settings.InputFile);
            }
            else
            {
                AnsiConsole.ErrorLine($"Could not find the specified file \"{settings.InputFile}\".");
                return -2;
            }
        }

        return 0;
    }

    private int AnalyzeMixFile(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken)
    {
        string currentFile = string.Empty;
        
        AnsiConsole.Progress()
            .Columns(new SpinnerColumn(), new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
            .AutoClear(true)
            .UseRenderHook((renderable, tasks) =>
            {
                var task = tasks.FirstOrDefault();

                if (task is null)
                    return renderable;

                return string.IsNullOrWhiteSpace(currentFile) ? renderable : new Rows(renderable, new Markup($"[bold]Processing:[/] {Markup.Escape(currentFile)}"));
            })
            .Start(ctx =>
            {
                var task = ctx.AddTask("Analyzing...", maxValue: MixFile.FileCount);
                task.HideWhenCompleted = true;
                
                for(int i = 0; i < MixFile.FileCount; ++i)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    var file = MixFile.Files[i];
                    var name = file.FileName;
                    var ext = Path.GetExtension(name)[1..].ToUpper();

                    currentFile = name;

                    try
                    {
                        switch (ext)
                        {
                            case "DDS":
                                Results.Add(i, ImageAnalyzer.AnalyzeDDS(file.Data));
                                break;

                            case "TGA":
                                Results.Add(i, ImageAnalyzer.AnalyzeTGA(file.Data));
                                break;

                            case "MP3":
                                Results.Add(i, AudioAnalyzer.AnalyzeMP3(file.Data));
                                break;

                            case "WAV":
                                Results.Add(i, AudioAnalyzer.AnalyzeWAV(file.Data));
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.ErrorLine($"Failed to analyze file {name}.");
                        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.NoStackTrace);
                    }
                    
                    task.Increment(1);
                }
                
                currentFile = string.Empty;
            });
        
        if (cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.ErrorLine("Aborting...");
            return 1;
        }

        return 0;
    }

    private IRenderable BuildOptimizationList(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken)
    {
        string color = "yellow";
        switch ((double)ActionCount / MixFile.FileCount)
        {
            case <0.5:
                color = "green";
                break;
            case >0.8:
                color = "red";
                break;
        }
            
        var tree = new Tree($"[{color}]{Path.GetFileName(settings.InputFile)}[/] [dim]([bold]{ActionCount}[/] optimizations)[/]");
            
        foreach (var entry in Results)
        {
            if (!entry.Value.NeedsAction) continue;

            var file = MixFile.Files[entry.Key];
            var name = file.FileName;
            var ext = Path.GetExtension(name)[1..].ToUpper();

            var fileNode = tree.AddNode(name);

            if (entry.Value is ImageAnalysisResult imageResult)
            {
                if (imageResult.NeedsConversion)
                {
                    fileNode.AddNode($"Format: [red]{ext}[/] --> [green]DDS[/]");
                }

                if (imageResult.NeedsResizing)
                {
                    fileNode.AddNode($"Size: [yellow]{imageResult.OldSize.Width}x{imageResult.OldSize.Height}[/] --> [green]{imageResult.NewSize.Width}x{imageResult.NewSize.Height}[/]");
                }
            }
            else if (entry.Value is AudioAnalysisResult audioResult)
            {
                if (audioResult.NeedsConversion)
                {
                    fileNode.AddNode($"Format: [red]{ext}[/] --> [green]MP3[/]");
                }

                if (audioResult.NeedsBitrateProcessing)
                {
                    fileNode.AddNode($"Bit Rate: [yellow]{audioResult.OldBitrate / 1000} kbps[/] --> [green]{audioResult.NewBitrate / 1000} kbps[/]");
                }
            }
        }

        return tree;
    }
    
    private int MakeBackup(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.ReadStandardInput)
        {
            AnsiConsole.Status()
                .Start($"Creating backup of [bold]{Path.GetFileName(settings.InputFile)}[/]...", ctx =>
                {
                    var backupFileName = Path.GetFileName(settings.InputFile) + "-BACKUP";
                    var backupPath = Path.Combine(Path.GetDirectoryName(settings.InputFile)!, backupFileName);
                    if (File.Exists(backupPath))
                    {
                        File.Delete(backupPath);
                    }

                    File.Copy(settings.InputFile, backupPath);
                    
                    AnsiConsole.MarkupLineInterpolated($"[green]Backup has been saved as [bold]{backupFileName}[/].[/]");
                });
        }

        return 0;
    }

    private int ApplyOptimizations(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken)
    {
        string currentFile = string.Empty;
        
        AnsiConsole.Progress()
            .Columns(new SpinnerColumn(), new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new RemainingTimeColumn())
            .AutoClear(true)
            .UseRenderHook((renderable, tasks) =>
            {
                var task = tasks.FirstOrDefault();

                if (task is null)
                    return renderable;
                
                return string.IsNullOrWhiteSpace(currentFile) ? renderable : new Rows(renderable, new Markup($"[bold]Processing:[/] {Markup.Escape(currentFile)}"));
            })
            .Start(ctx =>
            {
                var task = ctx.AddTask("Applying optimizations...", maxValue: ActionCount + 1);
                task.HideWhenCompleted = true;
                
                foreach (var entry in Results.Where(x => x.Value.NeedsAction))
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    var file = MixFile.Files[entry.Key];
                    var name = file.FileName;
                    var ext = Path.GetExtension(name)[1..].ToUpper();
                    
                    currentFile = name;

                    try
                    {
                        switch (ext)
                        {
                            case "DDS":
                                file.Data = ImageAnalyzer.ApplyDDS(file.Data, (ImageAnalysisResult)entry.Value);
                                break;

                            case "TGA":
                                if (((ImageAnalysisResult)entry.Value).NeedsConversion)
                                {
                                    file.FileName = Path.GetFileNameWithoutExtension(name) + ".dds";
                                }
                                file.Data = ImageAnalyzer.ApplyTGA(file.Data, (ImageAnalysisResult)entry.Value);
                                break;

                            case "MP3":
                                file.Data = AudioAnalyzer.ApplyMP3(file.Data, (AudioAnalysisResult)entry.Value);
                                break;

                            case "WAV":
                                var oldName = file.FileName;
                                file.FileName = Path.GetFileNameWithoutExtension(name) + ".mp3";
                                file.Data = AudioAnalyzer.ApplyWAV(file.Data, (AudioAnalysisResult)entry.Value);
                                LevelDataManipulator.ReplaceLevelData(MixFile, oldName, file.FileName);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.ErrorLine($"Failed to apply optimizations for {name}.");
                        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.NoStackTrace);
                    }

                    task.Increment(1);
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    task.Description = "Saving changes...";
                    currentFile = Path.GetFileName(settings.InputFile);
                    MixFile.Save(settings.OutputFile);
                    task.Increment(1);
                    currentFile = string.Empty;
                }
            });
        
        if (cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.ErrorLine("Aborting...");
            return 1;
        }

        return 0;
    }

    protected override int Execute(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken)
    {
        MixOptimize.Settings = settings;
        
        if (ReadMixFile(context, settings, cancellationToken, out var mixFileBytes) is var exit1 && exit1 != 0)
        {
            return exit1;
        }

        if (mixFileBytes.Length == 0)
        {
            AnsiConsole.ErrorLine("The specified Mix file is empty.");
            return -3;
        }
        
        try
        {
            MixFile = MixPackageClass.Load(mixFileBytes);
        }
        catch (Exception ex)
        {
            AnsiConsole.ErrorLine("Failed to load Mix file.");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.NoStackTrace);
            return -4;
        }

        if (EnsureFFMpegExists(context, settings, cancellationToken) is var exit2 && exit2 != 0)
        {
            return exit2;
        }
        
        Results = new();
        if (AnalyzeMixFile(context, settings, cancellationToken) is var exit3 && exit3 != 0)
        {
            return exit3;
        }
        
        if (ActionCount == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[green][bold]{Path.GetFileName(settings.InputFile)}[/] does not require any optimizations.[/]");
            return 0;
        }
            
        AnsiConsole.Write(BuildOptimizationList(context, settings, cancellationToken));

        if (!settings.SkipConfirmation)
        {
            if (!AnsiConsole.Confirm($"Would you like to apply all [bold]{ActionCount}[/] optimizations?"))
            {
                AnsiConsole.WriteLine("Exiting...");
                return 0;
            }
        }
        
        if (MakeBackup(context, settings, cancellationToken) is var exit4 && exit4 != 0)
        {
            return exit4;
        }

        if (ApplyOptimizations(context, settings, cancellationToken) is var exit5 && exit5 != 0)
        {
            return exit5;
        }
        
        AnsiConsole.MarkupLineInterpolated($"Applied [bold]{ActionCount}[/] optimizations to [green]{Path.GetFileName(settings.OutputFile)}[/].");
        return 0;
    }
}

public class MixOptimize
{
    public const string Name = nameof(MixOptimize);
    public const string Version = "1.1";
    public const string Authors = "Unstoppable";

    public static MixOptimizeSettings Settings { get; set; } = null!;
    
    static void PrintSplash()
    {
        Console.WriteLine($"{Name} utility {Version} - by {Authors}");
    }

    static int Main(string[] args)
    {
        PrintSplash();

        var app = new CommandApp<MixOptimizeCommand>();

        app.Configure(c =>
        {
            c.SetApplicationName(Name);
            c.SetApplicationVersion(Version);
            c.SetHelpProvider(new MixOptimizeHelpProvider(c.Settings));
        });

        return app.Run(args);
    }
}