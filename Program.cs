/*
 *  MixOptimize - C&C Renegade map and mod package optimizer
 *  Copyright (C) 2023 Unstoppable
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

using Spectre.Console.Cli.Help;
using Spectre.Console.Rendering;

namespace mixoptimize;

public class MixOptimizeHelpProvider : HelpProvider
{
    public MixOptimizeHelpProvider(ICommandAppSettings settings) : base(settings)
    {
        
    }

    public override IEnumerable<IRenderable> GetFooter(ICommandModel model, ICommandInfo? command)
    {
        var list = base.GetFooter(model, command).ToList();
        
        list.Add(new Rule());
        list.Add(new Markup("[blue]MixOptimize[/] is licensed under [bold]GNU General Public License v3.0[/]. Please view [b]LICENSE[/] file for details."));
        list.Add(new Markup("[blue]MixOptimize[/] uses the following open-source libraries:"));
        list.Add(new Rows(
            new Markup("[bold]Magick.NET[/] [dim]by[/] Dirk Lemstra"),
            new Markup("[bold]NAudio[/] [dim]by[/] Mark Heath & NAudio Contributors"),
            new Markup("[bold]NAudio.Lame.CrossPlatform[/] [dim]by[/] Corey Murtagh, Daniel Hilgarth"),
            new Markup("[bold]NLayer.NAudioSupport[/] [dim]by[/] Mark Heath, Andrew Ward"),
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
    
    [CommandOption("--out")]
    [Description("Output file. (Required if --read-stdin is specified)")]
    public string? OutputFile { get; set; }
}

public class MixOptimizeCommand : Command<MixOptimizeSettings>
{
    protected override int Execute(CommandContext context, MixOptimizeSettings settings, CancellationToken cancellationToken)
    {
        MixOptimize.Settings = settings;
        
        byte[] mixFileBytes;

        if (settings.ReadStandardInput)
        {
            settings.SkipConfirmation = true;
            
            if (settings.OutputFile == null)
            {
                AnsiConsole.ErrorLine("--out switch has to be specified when --read-stdin is used!");
                return -1;
            }

            settings.InputFile = "STDIN";

            using (var stdin = Console.OpenStandardInput())
            {
                byte[]? bytes = null;
                AnsiConsole.Status()
                    .Start("Reading the file from standard input...", async x =>
                    {
                        var result = await InputTask.ReadFromStream(stdin);
                        bytes = result;
                    });
                
                mixFileBytes = bytes ?? [];
            }
        }
        else
        {
            if (File.Exists(settings.InputFile))
            {
                settings.OutputFile = settings.InputFile;
                mixFileBytes = File.ReadAllBytes(settings.InputFile);
            }
            else
            {
                AnsiConsole.ErrorLine($"Could not find the specified file \"{settings.InputFile}\".");
                return -2;
            }
        }

        if (mixFileBytes.Length == 0)
        {
            AnsiConsole.ErrorLine("The specified Mix file is empty.");
            return -3;
        }

        MixPackageClass mixFile;
        try
        {
            mixFile = MixPackageClass.Load(mixFileBytes);
        }
        catch (Exception ex)
        {
            AnsiConsole.ErrorLine("Failed to load Mix file.");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything | ExceptionFormats.NoStackTrace);
            return -4;
        }

        Dictionary<int, IAnalysisResult> results = new();
        
        AnsiConsole.Progress()
            .Start(ctx =>
            {
                var task = ctx.AddTask("Analyzing the Mix file...", maxValue: mixFile.FileCount);
                task.HideWhenCompleted = true;
                
                var fileTask = ctx.AddTask("Initializing...", maxValue: 1);
                fileTask.IsIndeterminate = true;
                fileTask.HideWhenCompleted = true;
                
                for(int i = 0; i < mixFile.FileCount; ++i)
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    var file = mixFile.Files[i];
                    var name = file.FileName;
                    var ext = Path.GetExtension(name)[1..].ToUpper();

                    fileTask.Description = name;

                    try
                    {
                        switch (ext)
                        {
                            case "DDS":
                                results.Add(i, ImageAnalyzer.AnalyzeDDS(file.Data));
                                break;

                            case "TGA":
                                results.Add(i, ImageAnalyzer.AnalyzeTGA(file.Data));
                                break;

                            case "MP3":
                                results.Add(i, AudioAnalyzer.AnalyzeMP3(file.Data));
                                break;

                            case "WAV":
                                results.Add(i, AudioAnalyzer.AnalyzeWAV(file.Data));
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
                
                fileTask.Increment(1);
            });
        
        if (cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.ErrorLine("Aborting...");
            return 0;
        }

        var actionCount = results.Count(x => x.Value.NeedsAction);
        if (actionCount == 0)
        {
            AnsiConsole.MarkupLineInterpolated($"[green]Mix file {Path.GetFileName(settings.InputFile)} does not require any optimizations.[/]");
            return -5;
        }
        else
        {
            string color = "yellow";
            switch ((double)actionCount / mixFile.FileCount)
            {
                case <0.5:
                    color = "green";
                    break;
                case >0.8:
                    color = "red";
                    break;
            }
            
            var tree = new Tree($"[{color}]{Path.GetFileName(settings.InputFile)}[/] [dim]([bold]{actionCount}[/] optimizations)[/]");
            
            foreach (var entry in results)
            {
                if (!entry.Value.NeedsAction) continue;

                var file = mixFile.Files[entry.Key];
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
            
            AnsiConsole.Write(tree);
        }

        if (!settings.SkipConfirmation)
        {
            if (!AnsiConsole.Confirm($"Would you like to apply all [bold]{actionCount}[/] optimizations?"))
            {
                AnsiConsole.ErrorLine("Aborting...");
                return 0;
            }
        }

        // Create a backup of the original file.
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

        AnsiConsole.Progress()
            .Start(ctx =>
            {
                var task = ctx.AddTask("Applying optimizations...", maxValue: actionCount);
                task.HideWhenCompleted = true;
                
                var fileTask = ctx.AddTask("Initializing...", maxValue: 1);
                fileTask.IsIndeterminate = true;
                fileTask.HideWhenCompleted = true;
                
                foreach (var entry in results.Where(x => x.Value.NeedsAction))
                {
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    var file = mixFile.Files[entry.Key];
                    var name = file.FileName;
                    var ext = Path.GetExtension(name)[1..].ToUpper();

                    fileTask.Description = name;

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
                                LevelDataManipulator.ReplaceLevelData(mixFile, oldName, file.FileName);
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
                    fileTask.Description = "Saving Mix file...";
                    mixFile.Save(settings.OutputFile);
                    fileTask.Increment(1);
                }
            });
        
        if (cancellationToken.IsCancellationRequested)
        {
            AnsiConsole.ErrorLine("Aborting...");
            return 0;
        }
        
        AnsiConsole.MarkupLineInterpolated($"Applied [bold]{actionCount}[/] optimizations to [green]{Path.GetFileName(settings.OutputFile)}[/].");
        return 0;
    }
}

public class MixOptimize
{
    const string Version = "1.0";
    
    public static MixOptimizeSettings Settings { get; set; }
    
    static void PrintSplash()
    {
        Console.WriteLine($"MixOptimize utility {Version} - by Unstoppable");
    }

    static int Main(string[] args)
    {
        PrintSplash();

        var app = new CommandApp<MixOptimizeCommand>();

        app.Configure(c =>
        {
            c.SetApplicationName("MixOptimize");
            c.SetApplicationVersion(Version);
        });

        return app.Run(args);
    }
}